using Ryujinx.Common.Logging;
using Ryujinx.HLE.HOS.Kernel;
using Ryujinx.HLE.HOS.Kernel.Process;
using Ryujinx.HLE.HOS.Services.Hid;
using Ryujinx.HLE.HOS.Tamper;
using Ryujinx.Horizon.Sdk.Hid;
using Ryujinx.Horizon.Sdk.Hid.Npad;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Ryujinx.HLE.HOS
{
    public class TamperMachine
    {
        // Atmosphere specifies a delay of 83 milliseconds between the execution of the last
        // cheat and the re-execution of the first one.
        private const int TamperMachineSleepMs = 1000 / 12;

        private Thread _tamperThread = null;
        private readonly ConcurrentQueue<ITamperProgram> _programs = new();
        private long _pressedKeys = 0;
        private readonly Dictionary<string, ITamperProgram> _programDictionary = new();

        private void Activate()
        {
            if (_tamperThread == null || !_tamperThread.IsAlive)
            {
                _tamperThread = new Thread(this.TamperRunner)
                {
                    Name = "HLE.TamperMachine",
                };
                _tamperThread.Start();
            }
        }

        internal void InstallAtmosphereCheat(string name, string buildId, IEnumerable<string> rawInstructions, ProcessTamperInfo info, ulong exeAddress)
        {
            if (!CanInstallOnPid(info.Process.Pid))
            {
                return;
            }

            ITamperedProcess tamperedProcess = new TamperedKProcess(info.Process);
            AtmosphereCompiler compiler = new(exeAddress, info.HeapAddress, info.AliasAddress, info.AslrAddress, tamperedProcess);
            ITamperProgram program = compiler.Compile(name, rawInstructions);

            if (program != null)
            {
                program.TampersCodeMemory = false;

                _programs.Enqueue(program);
                _programDictionary.TryAdd($"{buildId}-{name}", program);
            }

            Activate();
        }

        private static bool CanInstallOnPid(ulong pid)
        {
            // Do not allow tampering of kernel processes.
            if (pid < KernelConstants.InitialProcessId)
            {
                Logger.Warning?.Print(LogClass.TamperMachine, $"Refusing to tamper kernel process {pid}");

                return false;
            }

            return true;
        }

        public void EnableCheats(string[] enabledCheats)
        {
            foreach (var program in _programDictionary.Values)
            {
                program.IsEnabled = false;
            }

            foreach (var cheat in enabledCheats)
            {
                if (_programDictionary.TryGetValue(cheat, out var program))
                {
                    program.IsEnabled = true;
                }
            }
        }

        private static bool IsProcessValid(ITamperedProcess process)
        {
            return process.State != ProcessState.Crashed && process.State != ProcessState.Exiting && process.State != ProcessState.Exited;
        }

        private void TamperRunner()
        {
            Logger.Info?.Print(LogClass.TamperMachine, "TamperMachine thread running");

            int sleepCounter = 0;

            while (true)
            {
                // Sleep to not consume too much CPU.
                if (sleepCounter == 0)
                {
                    sleepCounter = _programs.Count;
                    Thread.Sleep(TamperMachineSleepMs);
                }
                else
                {
                    sleepCounter--;
                }

                if (!AdvanceTamperingsQueue())
                {
                    // No more work to be done.

                    Logger.Info?.Print(LogClass.TamperMachine, "TamperMachine thread exiting");

                    return;
                }
            }
        }

        private bool AdvanceTamperingsQueue()
        {
            if (!_programs.TryDequeue(out ITamperProgram program))
            {
                // No more programs in the queue.
                _programDictionary.Clear();

                return false;
            }

            // Check if the process is still suitable for running the tamper program.
            if (!IsProcessValid(program.Process))
            {
                // Exit without re-enqueuing the program because the process is no longer valid.
                return true;
            }

            // Re-enqueue the tampering program because the process is still valid.
            _programs.Enqueue(program);

            Logger.Debug?.Print(LogClass.TamperMachine, $"Running tampering program {program.Name}");

            try
            {
                ControllerKeys pressedKeys = (ControllerKeys)Volatile.Read(ref _pressedKeys);
                program.Process.TamperedCodeMemory = false;
                program.Execute(pressedKeys);

                // Detect the first attempt to tamper memory and log it.
                if (!program.TampersCodeMemory && program.Process.TamperedCodeMemory)
                {
                    program.TampersCodeMemory = true;

                    Logger.Warning?.Print(LogClass.TamperMachine, $"Tampering program {program.Name} modifies code memory so it may not work properly");
                }
            }
            catch (Exception ex)
            {
                Logger.Debug?.Print(LogClass.TamperMachine, $"The tampering program {program.Name} crashed, this can happen while the game is starting");

                if (!string.IsNullOrEmpty(ex.Message))
                {
                    Logger.Debug?.Print(LogClass.TamperMachine, ex.Message);
                }
            }

            return true;
        }

        public void UpdateInput(List<GamepadInput> gamepadInputs)
        {
            // Look for the input of the player one or the handheld.
            foreach (GamepadInput input in gamepadInputs)
            {
                if (input.PlayerId == PlayerIndex.Player1 || input.PlayerId == PlayerIndex.Handheld)
                {
                    Volatile.Write(ref _pressedKeys, (long)input.Buttons);

                    return;
                }
            }

            // Clear the input because player one is not conected.
            Volatile.Write(ref _pressedKeys, 0);
        }
    }
}
