// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace System.Data.SqlClient.SNI
{
    /// <summary>
    /// SNI Packet
    /// </summary>
    internal class SNINetworkStream : Stream
    {
        private Socket _socket;

        public SNINetworkStream(Socket socket)
        {
            _socket = socket;
            _readArgs.Completed += ReadEvent;
            _writeArgs.Completed += WriteEvent;
        }

        public override bool CanRead => _socket.Connected;

        public override bool CanSeek => throw new NotImplementedException();

        public override bool CanWrite => _socket.Connected;

        public override long Length => throw new NotImplementedException();

        public override long Position { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public override void Flush()
        {
            return;
        }

       
        ManualResetEventSlim _readEvent = new ManualResetEventSlim();
        SocketAsyncEventArgs _readArgs = new SocketAsyncEventArgs();
        TaskCompletionSource<int> readTCS = new TaskCompletionSource<int>();

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken token)
        {
            readTCS = new TaskCompletionSource<int>();
            _readArgs.SetBuffer(buffer, offset, count);
            bool success = !_socket.ReceiveAsync(_readArgs);
            if(success)
            {
                return Task.FromResult(_readArgs.BytesTransferred);
            }
            return readTCS.Task;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {

            //_readArgs.SetBuffer(buffer, offset, count);
            //bool success = !_socket.ReceiveAsync(_readArgs);
            return _socket.Receive(buffer, offset, count, SocketFlags.None);
            /*
            if(success)
            {
                buffer = _readArgs.Buffer;
                return _readArgs.BytesTransferred;
            }
            // Wait for event
            _readEvent.Wait();
            _readEvent.Reset();
            return _readLength;
            */
        }

        private void ReadEvent(object sender, SocketAsyncEventArgs e)
        {
            Socket socket = (Socket)sender;
            if(e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                readTCS.SetResult(e.BytesTransferred);
            }
            else
            {
                e.AcceptSocket.Disconnect(false);
                readTCS.SetException(new IOException("Failed to write to socket"));
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        ManualResetEventSlim _writeEvent = new ManualResetEventSlim();
        SocketAsyncEventArgs _writeArgs = new SocketAsyncEventArgs();

        public override void Write(byte[] buffer, int offset, int count)
        {
            _socket.Send(buffer, offset, count, SocketFlags.None);
        }

        private void WriteEvent(object sender, SocketAsyncEventArgs e)
        {
            Socket socket = (Socket)sender;
            if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
            {
                _writeTcs.SetResult(null);
            }
            else
            {
                e.AcceptSocket.Disconnect(false);
                _writeTcs.SetException(new IOException("Failed to send data on socket"));
            }
        }

        private TaskCompletionSource<object> _writeTcs = new TaskCompletionSource<object>();

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            _writeTcs = new TaskCompletionSource<object>();
            _writeArgs.SetBuffer(buffer, offset, count);

            if (!_socket.SendAsync(_writeArgs))
            {
                return Task.FromResult<object>(null);
            }
            return _writeTcs.Task;
        }
    }
}
