// Source: com.vrcfury.vrcfury/Editor-Avatars/Service/Compressor/ParameterCompressorLayerService.cs
// Changes:
//   - DI replaced by constructor args.
//   - VRCFury's VFLayer/VFState/VFTransitionBuilder DSL was replaced by NDMF's VirtualLayer/VirtualStateMachine/VirtualState/VirtualStateTransition.
//     The state graph, the transition order, the drivers and the state positions are otherwise unchanged:
//       fx.NewLayer(name)                  -> fx.AddLayer(priority, name)
//       layer.NewState(name).Move(a, x, y) -> stateMachine.AddState(name, position: ...)
//       a.TransitionsTo(b).When(cond)      -> a.TransitionsTo(b, cond)          (VFStateAvatarExtensions)
//       .WithTransitionExitTime(t).When()  -> a.TransitionsTo(b, exitTime: t)
//       fx.NewBool(n, synced: true)        -> layerUtilsService.MakeSyncedBool(n)
//     The layer is added with the highest layer priority so it stays last in FX, matching VRCFury's behaviour of appending the layer.
//   - The first state created is explicitly made the state machine's default state (VRCFury's VFLayer did this implicitly).

using System;
using System.Collections.Generic;
using System.Linq;
using FOSA.FPC.Utils;
using FOSA.FPC.Utils.Controller;
using nadena.dev.ndmf.animator;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace FOSA.FPC.Service.Compressor {
    /**
     * Builds the FX layer responsible for actually handling the parameter compression.
     */
    internal class ParameterCompressorLayerService {
        private readonly CompressorLayerUtilsService layerUtilsService;
        private readonly VirtualAnimatorController fx;

        // VRCFury's VFState.X_OFFSET / Y_OFFSET, used to keep the generated graph readable.
        private const float X_OFFSET = 250;
        private const float Y_OFFSET = 80;

        public ParameterCompressorLayerService(CompressorLayerUtilsService layerUtilsService, VirtualAnimatorController fx) {
            this.layerUtilsService = layerUtilsService;
            this.fx = fx;
        }

        public void BuildLayer(OptimizationDecision decision) {
            var (numberBatches, boolBatches) = decision.GetBatches();

            var batchCount = Math.Max(numberBatches.Count, boolBatches.Count);
            var indexBitCount = decision.GetIndexBitCount();
            var syncIndex = Enumerable.Range(0, indexBitCount)
                .Select(i => layerUtilsService.MakeSyncedBool($"SyncIndex{i}"))
                .ToArray();
            var syncInts = Enumerable.Range(0, decision.numberSlots)
                .Select(i => layerUtilsService.MakeParam("SyncDataNum" + i, VRCExpressionParameters.ValueType.Int, true))
                .ToList();
            var syncBools = Enumerable.Range(0, decision.boolSlots)
                .Select(i => layerUtilsService.MakeParam("SyncDataBool" + i, VRCExpressionParameters.ValueType.Bool, true))
                .ToList();

            var layer = fx.AddLayer(new LayerPriority(int.MaxValue), "Parameter Compressor");
            layer.DefaultWeight = 0;
            var stateMachine = layer.StateMachine;

            var entryPos = Offset(Vector3.zero, 0, 2);
            var entry = stateMachine.AddState("Entry", position: entryPos);
            stateMachine.DefaultState = entry;
            var remoteLost = stateMachine.AddState("Receive (Lost)", position: Offset(entryPos, 0, 1));
            entry.TransitionsTo(remoteLost, layerUtilsService.IsLocal().IsFalse());
            var local = stateMachine.AddState("Local", position: Offset(entryPos, -2, 1));
            entry.TransitionsTo(local, layerUtilsService.Always());

            Action applyLatchDrivers = () => { };
            Action applyCopyDrivers = () => { };
            Action applyUnlatchDrivers = () => { };
            Action applyLoopTransition = () => { };
            Action<VirtualState,VirtualState,VFCondition> whenNextStateReady = null;
            VirtualState latchState = null;
            VirtualState unlatchState = null;
            var yOffset = 2;
            foreach (var batchNum in Enumerable.Range(0, batchCount)) {
                Action<VirtualState> sendIdDriver = s => { };

                var syncId = batchNum + 1;
                if (syncId >= (1 << indexBitCount)) throw new Exception("Unexpected syncId outside of index bit range");
                var syncIds = Enumerable.Range(0, indexBitCount).Select(i => (syncId & 1<<(indexBitCount-1-i)) > 0).ToArray();
                var titleId = syncIds.Select(b => b ? "1" : "0").Join("");
                var receiveConditions = new List<VFCondition>();
                foreach (var i in Enumerable.Range(0, indexBitCount)) {
                    sendIdDriver += s => s.Drives(syncIndex[i], syncIds[i]);
                    receiveConditions.Add(syncIndex[i].Is(syncIds[i]));
                }
                var receiveCondition = VFCondition.All(receiveConditions);

                var numberBatch = batchNum < numberBatches.Count ? numberBatches[batchNum] : new List<VRCExpressionParameters.Parameter>();
                var boolBatch = batchNum < boolBatches.Count ? boolBatches[batchNum] : new List<VRCExpressionParameters.Parameter>();

                // What is this sync index called?
                var title = $"({titleId}):\n" + numberBatch.Concat(boolBatch).Select(p => p.name).Join("\n");

                var latchNow = batchNum == 0;
                var unlatchNow = batchNum == batchCount - 1;

                var sendState = stateMachine.AddState($"{(latchNow ? "Latch & " : "")}Send {title}", position: Offset(entryPos, -1, yOffset));
                var receiveState = stateMachine.AddState($"Receive{(unlatchNow ? " & Unlatch" : "")} {title}", position: Offset(entryPos, 1, yOffset));

                // We can't just go to the next send after 0.1s, because of a weird unity animator quirk where it will exit
                // "early" if it thinks the exit time is closer to the current frame than the next frame, which would potentially
                // make it update faster than the sync rate and lose a packet. To fix this, we have to add exactly one extra frame by going through
                // an additional state between each send.
                var sendStateExtraFrame = stateMachine.AddState("Extra Frame", position: Offset(entryPos, -2, yOffset));

                if (batchNum == 0) {
                    local.TransitionsTo(sendState, layerUtilsService.Always());
                    remoteLost.TransitionsTo(receiveState, receiveCondition);
                    applyLoopTransition += () => {
                        whenNextStateReady?.Invoke(sendStateExtraFrame, receiveState, receiveCondition);
                    };
                } else {
                    whenNextStateReady?.Invoke(sendStateExtraFrame, receiveState, receiveCondition);
                }
                whenNextStateReady = (nextSend, nextRecv, nextRecvCond) => {
                    sendState.TransitionsTo(nextSend, exitTime: ParameterCompressorService.BATCH_TIME);
                    receiveState.TransitionsTo(nextRecv, nextRecvCond);
                    receiveState.TransitionsTo(remoteLost, receiveCondition.Not().And(nextRecvCond.Not()));
                };
                if (latchNow) latchState = sendState;
                if (unlatchNow) unlatchState = receiveState;

                sendIdDriver.Invoke(sendState);
                sendStateExtraFrame.TransitionsTo(sendState, layerUtilsService.Always());

                void SyncParam(VRCExpressionParameters.Parameter original, VRCExpressionParameters.Parameter slot) {
                    VRCExpressionParameters.Parameter sendFrom;
                    VRCExpressionParameters.Parameter receiveTo;
                    var latch = new Lazy<VRCExpressionParameters.Parameter>(() => layerUtilsService.MakeParam(original.name, original.valueType, false));
                    if (latchNow) {
                        // No need to latch things in the first batch when sending
                        sendFrom = original;
                    } else {
                        applyLatchDrivers += () => latchState?.DrivesCopy(original.name, latch.Value.name);
                        sendFrom = latch.Value;
                    }
                    if (unlatchNow) {
                        receiveTo = original;
                    } else {
                        applyUnlatchDrivers += () => unlatchState?.DrivesCopy(latch.Value.name, original.name);
                        receiveTo = latch.Value;
                    }
                    if (original.valueType == VRCExpressionParameters.ValueType.Float) {
                        applyCopyDrivers += () => {
                            sendState.DrivesCopy(sendFrom.name, slot.name, -1, 1, 0, 254);
                            receiveState.DrivesCopy(slot.name, receiveTo.name, 0, 254, -1, 1);
                        };
                    } else if (original.valueType == VRCExpressionParameters.ValueType.Int || original.valueType == VRCExpressionParameters.ValueType.Bool) {
                        applyCopyDrivers += () => {
                            sendState.DrivesCopy(sendFrom.name, slot.name);
                            receiveState.DrivesCopy(slot.name, receiveTo.name);
                        };
                    } else {
                        throw new Exception("Unknown type?");
                    }
                }
                foreach (var slotNum in Enumerable.Range(0, numberBatch.Count())) {
                    SyncParam(numberBatch[slotNum], syncInts[slotNum]);
                }
                foreach (var slotNum in Enumerable.Range(0, boolBatch.Count())) {
                    SyncParam(boolBatch[slotNum], syncBools[slotNum]);
                }

                yOffset += (int)Math.Ceiling((title.Split('\n').Length+1) / 5f);
            }
            applyLoopTransition.Invoke();
            applyLatchDrivers.Invoke();
            applyCopyDrivers.Invoke();
            applyUnlatchDrivers.Invoke();
            layerUtilsService.FixWd(layer);
        }

        private static Vector3 Offset(Vector3 basis, float x, float y) {
            return new Vector3(basis.x + x * X_OFFSET, basis.y + y * Y_OFFSET, basis.z);
        }
    }
}
