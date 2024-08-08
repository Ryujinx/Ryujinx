using Ryujinx.SDL2.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using static SDL2.SDL;

namespace Ryujinx.Input.SDL2
{
    public class SDL2GamepadDriver : IGamepadDriver
    {
        private readonly Dictionary<int, string> _gamepadsInstanceIdsMapping;
        private readonly List<string> _gamepadsIds;
        private readonly Lock _lock = new();

        public ReadOnlySpan<string> GamepadsIds
        {
            get
            {
                lock (_lock)
                {
                    return _gamepadsIds.ToArray();
                }
            }
        }

        public string DriverName => "SDL2";

        public event Action<string> OnGamepadConnected;
        public event Action<string> OnGamepadDisconnected;

        public SDL2GamepadDriver()
        {
            _gamepadsInstanceIdsMapping = new Dictionary<int, string>();
            _gamepadsIds = new List<string>();

            SDL2Driver.Instance.Initialize();
            SDL2Driver.Instance.OnJoyStickConnected += HandleJoyStickConnected;
            SDL2Driver.Instance.OnJoystickDisconnected += HandleJoyStickDisconnected;

            // Add already connected gamepads
            int numJoysticks = SDL_NumJoysticks();

            for (int joystickIndex = 0; joystickIndex < numJoysticks; joystickIndex++)
            {
                HandleJoyStickConnected(joystickIndex, SDL_JoystickGetDeviceInstanceID(joystickIndex));
            }
        }

        private string GenerateGamepadId(int joystickIndex)
        {
            Guid guid = SDL_JoystickGetDeviceGUID(joystickIndex);

            // Add a unique identifier to the start of the GUID in case of duplicates.

            if (guid == Guid.Empty)
            {
                return null;
            }

            string id;

            lock (_lock)
            {
                int guidIndex = 0;
                id = guidIndex + "-" + guid;

                while (_gamepadsIds.Contains(id))
                {
                    id = (++guidIndex) + "-" + guid;
                }
            }

            return id;
        }

        private int GetJoystickIndexByGamepadId(string id)
        {
            lock (_lock)
            {
                return _gamepadsIds.IndexOf(id);
            }
        }

        private void HandleJoyStickDisconnected(int joystickInstanceId)
        {
            if (_gamepadsInstanceIdsMapping.TryGetValue(joystickInstanceId, out string id))
            {
                _gamepadsInstanceIdsMapping.Remove(joystickInstanceId);

                lock (_lock)
                {
                    _gamepadsIds.Remove(id);
                }

                OnGamepadDisconnected?.Invoke(id);
            }
        }

        private void HandleJoyStickConnected(int joystickDeviceId, int joystickInstanceId)
        {
            if (SDL_IsGameController(joystickDeviceId) == SDL_bool.SDL_TRUE)
            {
                if (_gamepadsInstanceIdsMapping.ContainsKey(joystickInstanceId))
                {
                    // Sometimes a JoyStick connected event fires after the app starts even though it was connected before
                    // so it is rejected to avoid doubling the entries.
                    return;
                }

                string id = GenerateGamepadId(joystickDeviceId);

                if (id == null)
                {
                    return;
                }

                if (_gamepadsInstanceIdsMapping.TryAdd(joystickInstanceId, id))
                {
                    lock (_lock)
                    {
                        _gamepadsIds.Add(id);
                    }

                    OnGamepadConnected?.Invoke(id);
                }
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                SDL2Driver.Instance.OnJoyStickConnected -= HandleJoyStickConnected;
                SDL2Driver.Instance.OnJoystickDisconnected -= HandleJoyStickDisconnected;

                // Simulate a full disconnect when disposing
                foreach (string id in _gamepadsIds)
                {
                    OnGamepadDisconnected?.Invoke(id);
                }

                lock (_lock)
                {
                    _gamepadsIds.Clear();
                }

                SDL2Driver.Instance.Dispose();
            }
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        public IGamepad GetGamepad(string id)
        {
            int joystickIndex = GetJoystickIndexByGamepadId(id);

            if (joystickIndex == -1)
            {
                return null;
            }

            IntPtr gamepadHandle = SDL_GameControllerOpen(joystickIndex);

            if (gamepadHandle == IntPtr.Zero)
            {
                return null;
            }

            return new SDL2Gamepad(gamepadHandle, id);
        }
    }
}
