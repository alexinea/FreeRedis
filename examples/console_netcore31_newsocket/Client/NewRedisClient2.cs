﻿using Microsoft.AspNetCore.Connections;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;

namespace console_netcore31_newsocket
{
    public class TaskWrapper
    {
        public readonly byte[] Bytes;
        public readonly TaskCompletionSource<bool> Task;
        public TaskWrapper(byte[] bytes, TaskCompletionSource<bool> task)
        {
            Bytes = bytes;
            Task = task;
        }
    }
    public class NewRedisClient2
    {

        private readonly ConcurrentQueue<TaskWrapper> _sendQueue;
        private readonly ConcurrentQueue<TaskCompletionSource<bool>> _receiverQueue;
        private readonly byte _protocalStart;
        private readonly ConnectionContext _connection;
        public readonly PipeWriter _sender;
        private readonly PipeReader _reciver;
        public NewRedisClient2(string ip, int port) : this(new IPEndPoint(IPAddress.Parse(ip), port))
        {
        }
        public NewRedisClient2(IPEndPoint point)
        {
            _protocalStart = (byte)43;
            _sendQueue = new ConcurrentQueue<TaskWrapper>();
            _receiverQueue = new ConcurrentQueue<TaskCompletionSource<bool>>();
            SocketConnectionFactory client = new SocketConnectionFactory(new SocketTransportOptions());
            _connection = client.ConnectAsync(point).Result;
            _sender = _connection.Transport.Output;
            _reciver = _connection.Transport.Input;
            RunReciver();
            Task.Run(async () => { 
                await Task.Delay(30000);
                Console.WriteLine(total);
                Console.WriteLine(_receiverQueue.Count);
                await Task.Delay(20000);
                Console.WriteLine(total);
                Console.WriteLine(_receiverQueue.Count);
                await Task.Delay(10000);
                Console.WriteLine(total);
                Console.WriteLine(_receiverQueue.Count);
            });
            //RunSender();
        }

        private TaskCompletionSource<bool> _sendTask;
        public async Task<bool> SetAsync(string key,string value)
        {
            return await SendAsync($"SET {key} {value}\r\n");
        }
        private readonly object _lock = new object();
        private int _taskCount;
        public Task<bool> SendAsync(string value)
        {
            
            var bytes = Encoding.UTF8.GetBytes(value);
            var taskSource = new TaskCompletionSource<bool>();
            lock (_lock)
            {
                //Interlocked.Increment(ref _taskCount);
                _receiverQueue.Enqueue(taskSource);
                _sender.WriteAsync(bytes);
            }
            
            return taskSource.Task;

        }
        long total = 0;
        private async void RunSender()
        {

            TaskWrapper task;
            while (true)
            {

                if (_sendQueue.IsEmpty)
                {

                    _sendTask = new TaskCompletionSource<bool>();
                    await _sendTask.Task.ConfigureAwait(false);

                }
                int count = 0;
                while (!_sendQueue.IsEmpty)
                {
                    count += 1;
                    if (count == 1000)
                    {
                        Console.WriteLine("count = 1000");
                        count = 0;
                        await _sender.FlushAsync();
                    }
                    while (!_sendQueue.TryDequeue(out task)) { };
                    await _sender.WriteAsync(task.Bytes).ConfigureAwait(false);
                    //_sender.Advance(task.Bytes.Length);
                    _receiverQueue.Enqueue(task.Task);
                    
                }


            }
        }
        private async void RunReciver()
        {
            
            while (true)
            {

                var result = await _reciver.ReadAsync();
                var buffer = result.Buffer;
                
                if (buffer.IsSingleSegment)
                {
                    
                    //total += buffer.Length;
                    //Console.WriteLine($"当前剩余 {_taskCount} 个任务未完成,队列中有 {_receiverQueue.Count} 个任务！缓冲区长 {buffer.Length} .");
                    Handler(buffer);
                }
                else
                {
                   
                    //total += buffer.Length;
                    //Console.WriteLine($"当前剩余 {_taskCount} 个任务未完成,队列中有 {_receiverQueue.Count} 个任务！缓冲区长 {buffer.Length} .");
                    Handler(buffer);
                }
                _reciver.AdvanceTo(buffer.End);
                if (result.IsCompleted)
                {
                    return;
                }
                
            }
        }


        private void Handler(in ReadOnlySequence<byte> sequence)
        {
            TaskCompletionSource<bool> task;
            var reader = new SequenceReader<byte>(sequence);
            //int _deal = 0;
            //79 75
            //if (reader.TryReadTo(out ReadOnlySpan<byte> result, 43, advancePastDelimiter: true))
            //{
                while (reader.TryReadTo(out ReadOnlySpan<byte> _, 43, advancePastDelimiter: true))
                {

                    while (!_receiverQueue.TryDequeue(out task)) { }
                    //_deal += 1;
                    //Interlocked.Decrement(ref _taskCount);
                    task.SetResult(true);
                }
           // }
            //while (!_receiverQueue.TryDequeue(out task)) { }
            //Interlocked.Increment(ref count);
            //task.SetResult(Encoding.UTF8.GetString(sequence.Slice(reader.Position, sequence.End).ToArray()).Contains("OK"));
        }
        private int count;
        private void Handler(in ReadOnlySpan<byte> span)
        {

            var tempSpan = span;
            TaskCompletionSource<bool> task = default;
            //var reader = new SequenceReader<byte>(new ReadOnlySequence<byte>(span));
            //var offset = tempSpan.IndexOf(_protocalStart);
            int offset;
            //int _deal = 0;
            while ((offset = tempSpan.IndexOf(_protocalStart)) != -1)
            {

                tempSpan = tempSpan.Slice(offset + 1, tempSpan.Length - offset -1);
                while (!_receiverQueue.TryDequeue(out task)) { }

                //if (task != default)
                //{

                    //_deal += 1;
                    //Interlocked.Decrement(ref _taskCount);
                    task.SetResult(true);
                    //task = default;
                //}
                
            }
            //Console.WriteLine($"本次完成 {_deal} 个任务! 剩余 {_taskCount} 个任务！");
        }

        //public async Task<bool> AuthAsync(string password)
        //{
        //    var result = await SendAsync($"AUTH {password}\r\n");
        //    return result == "OK\r\n";
        //}

        //public async Task<bool> SelectDB(int dbIndex)
        //{
        //    var result = await SendAsync($"SELECT {dbIndex}\r\n");
        //    return result == "OK\r\n";
        //}
        //public async Task<bool> Set(string key, string value)
        //{
        //    var result = await SendAsync(new List<object> { "SET", key, value });
        //    return result == "OK\r\n";
        //    //var result = await SendAsync($"SET {key} {value}\r\n");
        //    //return result == "OK\r\n";
        //}
        //public async Task<string> Get(string key)
        //{
        //    var result = await SendAsync(new List<object> { "GET", key });
        //    return result;
        //    //var result = await SendAsync($"SET {key} {value}\r\n");
        //    //return result == "OK\r\n";
        //}
        //public async Task<bool> PingAsync()
        //{
        //    return await SendAsync($"PING\r\n") == "PONG\r\n";
        //}
    }
}
