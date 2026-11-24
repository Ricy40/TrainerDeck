using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;
using Windows.Data.Json;

namespace TrainerDeck.Widget
{
    internal sealed class DeckClient : IDisposable
    {
        private const string PipePath = @"\\.\pipe\TrainerDeck.GameBarWidget";

        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private FileStream _stream;
        private StreamReader _reader;
        private StreamWriter _writer;

        public Task<JsonObject> StateAsync() => SendAsync("{\"op\":\"state\"}");

        public Task<JsonObject> ActivateAsync(int row) => SendAsync("{\"op\":\"activate\",\"row\":" + row + "}");

        public Task<JsonObject> AdjustAsync(int row, int delta) => SendAsync("{\"op\":\"adjust\",\"row\":" + row + ",\"delta\":" + delta + "}");

        public Task<JsonObject> OpenAsync() => SendAsync("{\"op\":\"open\"}");

        // Windows error from the last failed connect: 2 = Trainer Deck isn't running, 5 = access denied, 231 = pipe busy.
        public int LastError { get; private set; }

        private async Task<JsonObject> SendAsync(string request)
        {
            await _gate.WaitAsync();
            try
            {
                return await Task.Run(() =>
                {
                    try
                    {
                        LastError = 0;
                        EnsureConnected();
                        _writer.Write(request + "\n");
                        _writer.Flush();
                        string line = _reader.ReadLine();
                        if (line == null)
                        {
                            Reset();
                            return null;
                        }

                        return JsonObject.TryParse(line, out JsonObject state) ? state : null;
                    }
                    catch (IOException)
                    {
                        Reset();
                        return null;
                    }
                });
            }
            finally
            {
                _gate.Release();
            }
        }

        private void EnsureConnected()
        {
            if (_stream != null)
            {
                return;
            }

            IntPtr handle = CreateFileFromAppW(PipePath, GenericRead | GenericWrite, 0, IntPtr.Zero, OpenExisting, 0, IntPtr.Zero);
            if (handle == new IntPtr(-1))
            {
                LastError = Marshal.GetLastWin32Error();
                throw new IOException("Couldn't open Trainer Deck's pipe (" + LastError + ").");
            }

            var encoding = new UTF8Encoding(false);
            _stream = new FileStream(new SafeFileHandle(handle, true), FileAccess.ReadWrite, 1);
            _reader = new StreamReader(_stream, encoding, false, 4096);
            _writer = new StreamWriter(_stream, encoding, 4096);
        }

        private void Reset()
        {
            _reader?.Dispose();
            _writer?.Dispose();
            _stream?.Dispose();
            _reader = null;
            _writer = null;
            _stream = null;
        }

        public void Dispose() => Reset();

        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint OpenExisting = 3;

        [DllImport("api-ms-win-core-file-fromapp-l1-1-0.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateFileFromAppW(string fileName, uint desiredAccess, uint shareMode, IntPtr securityAttributes,
            uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);
    }
}
