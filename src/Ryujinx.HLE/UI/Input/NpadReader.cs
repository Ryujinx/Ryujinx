namespace Ryujinx.HLE.UI.Input
{
    /// <summary>
    /// Class that converts Hid entries for the Npad into pressed / released events.
    /// </summary>
    class NpadReader
    {
        private readonly Switch _device;
        private readonly NpadCommonState[] _lastStates;

        public event NpadButtonHandler NpadButtonUpEvent;
        public event NpadButtonHandler NpadButtonDownEvent;

        public NpadReader(Switch device)
        {
            _device = device;
            _lastStates = new NpadCommonState[_device.Hid.SharedMemory.Npads.Length];
        }

        public NpadButton GetCurrentButtonsOfNpad(int npadIndex)
        {
            return _lastStates[npadIndex].Buttons;
        }

        public NpadButton GetCurrentButtonsOfAllNpads()
        {
            NpadButton buttons = 0;

            foreach (var state in _lastStates)
            {
                buttons |= state.Buttons;
            }

            return buttons;
        }

        private static ref RingLifo<NpadCommonState> GetCommonStateLifo(ref NpadInternalState npad)
        {
            switch (npad.StyleSet)
            {
                case NpadStyleTag.FullKey:
                    return ref npad.FullKey;
                case NpadStyleTag.Handheld:
                    return ref npad.Handheld;
                case NpadStyleTag.JoyDual:
                    return ref npad.JoyDual;
                case NpadStyleTag.JoyLeft:
                    return ref npad.JoyLeft;
                case NpadStyleTag.JoyRight:
                    return ref npad.JoyRight;
                case NpadStyleTag.Palma:
                    return ref npad.Palma;
                default:
                    return ref npad.SystemExt;
            }
        }

        public void Update(bool supressEvents = false)
        {
            ref var npads = ref _device.Hid.SharedMemory.Npads;

            // Process each input individually.
            for (int npadIndex = 0; npadIndex < npads.Length; npadIndex++)
            {
                UpdateNpad(npadIndex, supressEvents);
            }
        }

        private void UpdateNpad(int npadIndex, bool supressEvents)
        {
            const int MaxEntries = 1024;

            ref var npadState = ref _device.Hid.SharedMemory.Npads[npadIndex];
            ref var lastEntry = ref _lastStates[npadIndex];

            var fullKeyEntries = GetCommonStateLifo(ref npadState.InternalState).ReadEntries(MaxEntries);

            int firstEntryNum;

            // Scan the LIFO for the first entry that is newer that what's already processed.
            for (firstEntryNum = fullKeyEntries.Length - 1;
                 firstEntryNum >= 0 && fullKeyEntries[firstEntryNum].Object.SamplingNumber <= lastEntry.SamplingNumber;
                 firstEntryNum--)
            {
            }

            if (firstEntryNum == -1)
            {
                return;
            }

            for (; firstEntryNum >= 0; firstEntryNum--)
            {
                var entry = fullKeyEntries[firstEntryNum];

                // The interval of valid entries should be contiguous.
                if (entry.SamplingNumber < lastEntry.SamplingNumber)
                {
                    break;
                }

                if (!supressEvents)
                {
                    ProcessNpadButtons(npadIndex, entry.Object.Buttons);
                }

                lastEntry = entry.Object;
            }
        }

        private void ProcessNpadButtons(int npadIndex, NpadButton buttons)
        {
            NpadButton lastButtons = _lastStates[npadIndex].Buttons;

            for (ulong buttonMask = 1; buttonMask != 0; buttonMask <<= 1)
            {
                NpadButton currentButton = (NpadButton)buttonMask & buttons;
                NpadButton lastButton = (NpadButton)buttonMask & lastButtons;

                if (lastButton != 0)
                {
                    if (currentButton == 0)
                    {
                        NpadButtonUpEvent?.Invoke(npadIndex, lastButton);
                    }
                }
                else
                {
                    if (currentButton != 0)
                    {
                        NpadButtonDownEvent?.Invoke(npadIndex, currentButton);
                    }
                }
            }
        }
    }
}
