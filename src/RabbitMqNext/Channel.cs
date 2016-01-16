﻿namespace RabbitMqNext
{
	using System;
	using System.Collections.Concurrent;
	using System.Collections.Generic;
	using System.IO;
	using System.Threading;
	using System.Threading.Tasks;
	using Internals;
	using Internals.RingBuffer;

	public class Channel : IDisposable
	{
		private readonly CancellationToken _cancellationToken;
		internal readonly ChannelIO _io;
		internal MessagesPendingConfirmationKeeper _confirmationKeeper;

		private readonly ConcurrentDictionary<string, BasicConsumerSubscriptionInfo> _consumerSubscriptions;

		private readonly ObjectPool<BasicProperties> _propertiesPool;

		public Channel(ushort channelNumber, ConnectionIO connectionIo, CancellationToken cancellationToken)
		{
			_cancellationToken = cancellationToken;
			_io = new ChannelIO(this, channelNumber, connectionIo)
			{
				OnError = error =>
				{
					var ev = this.OnError;
					if (ev != null) ev(error);
				}
			};

			_consumerSubscriptions = new ConcurrentDictionary<string, BasicConsumerSubscriptionInfo>(StringComparer.Ordinal);

			_propertiesPool = new ObjectPool<BasicProperties>(() => new BasicProperties(false, reusable: true), 100, preInitialize: false);
		}

		public event Action<AmqpError> OnError;

		public bool IsConfirmationEnabled
		{
			get { return _confirmationKeeper != null; }
		}

		public ushort ChannelNumber
		{
			get { return _io.ChannelNumber; }
		}

		public bool IsClosed { get { return _io.IsClosed; } }

		public Func<UndeliveredMessage, Task> MessageUndeliveredHandler;

		public BasicProperties RentBasicProperties()
		{
			return _propertiesPool.GetObject();
		}

		public void Return(BasicProperties properties)
		{
			if (properties == null) throw new ArgumentNullException("properties");
			if (!properties.IsReusable) return;

			_propertiesPool.PutObject(properties);
		}

		public Task BasicQos(uint prefetchSize, ushort prefetchCount, bool global)
		{
			return _io.__BasicQos(prefetchSize, prefetchCount, global);
		}

		public Task BasicAck(ulong deliveryTag, bool multiple)
		{
			return _io.__BasicAck(deliveryTag, multiple);
		}

		public Task BasicNAck(ulong deliveryTag, bool multiple, bool requeue)
		{
			return _io.__BasicNAck(deliveryTag, multiple, requeue);
		}

		public Task ExchangeDeclare(string exchange, string type, bool durable, bool autoDelete,
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return _io.__ExchangeDeclare(exchange, type, durable, autoDelete, arguments, waitConfirmation);
		}

		public Task<AmqpQueueInfo> QueueDeclare(string queue, bool passive, bool durable, bool exclusive, bool autoDelete,
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			return _io.__QueueDeclare(queue, passive, durable, exclusive, autoDelete, arguments, waitConfirmation);
		}

		public Task QueueBind(string queue, string exchange, string routingKey, IDictionary<string, object> arguments,
			bool waitConfirmation)
		{
			return _io.__QueueBind(queue, exchange, routingKey, arguments, waitConfirmation);
		}

		public void BasicPublishFast(string exchange, string routingKey, bool mandatory, bool immediate,
			BasicProperties properties, ArraySegment<byte> buffer)
		{
			_io.__BasicPublish(exchange, routingKey, mandatory, immediate, properties, buffer, false);
		}

		public TaskSlim BasicPublish(string exchange, string routingKey, bool mandatory, bool immediate,
			BasicProperties properties, ArraySegment<byte> buffer)
		{
			return _io.__BasicPublish(exchange, routingKey, mandatory, immediate, properties, buffer, true);
		}

		public Task<string> BasicConsume(ConsumeMode mode, Func<MessageDelivery, Task> consumer,
			string queue, string consumerTag, bool withoutAcks, bool exclusive,
			IDictionary<string, object> arguments, bool waitConfirmation)
		{
			if (consumer == null) throw new ArgumentNullException("consumer");
			if (!waitConfirmation && string.IsNullOrEmpty(consumerTag)) 
				throw new ArgumentException("You must specify a consumer tag if waitConfirmation = false");

			if (!string.IsNullOrEmpty(consumerTag))
			{
				_consumerSubscriptions[consumerTag] = new BasicConsumerSubscriptionInfo
				{
					Mode = mode,
					Callback = consumer
				};
			}

			return _io.__BasicConsume(mode, queue, consumerTag, withoutAcks, exclusive, arguments, waitConfirmation,
				consumerTag2 =>
				{
					_consumerSubscriptions[consumerTag2] = new BasicConsumerSubscriptionInfo
					{
						Mode = mode,
						Callback = consumer
					};
				});
		}

		public Task BasicCancel(string consumerTag, bool waitConfirmation)
		{
			return _io.__BasicCancel(consumerTag, waitConfirmation);
		}

		public Task BasicRecover(bool requeue)
		{
			return _io.__BasicRecover(requeue);
		}

		public async Task<RpcHelper> CreateRpcHelper(ConsumeMode mode, int maxConcurrentCalls = 500)
		{
			var helper = new RpcHelper(this, maxConcurrentCalls, mode);
			await helper.Setup();
			return helper;
		}

		public async Task Close()
		{
			await this._io.InitiateCleanClose(false, null);

			this.Dispose();
		}

		public void Dispose()
		{
			this._io.Dispose();
		}

		internal async Task DispatchDeliveredMessage(string consumerTag, ulong deliveryTag, bool redelivered, 
			string exchange, string routingKey,	int bodySize, BasicProperties properties, Stream stream)
		{
			BasicConsumerSubscriptionInfo consumer;

			if (_consumerSubscriptions.TryGetValue(consumerTag, out consumer))
			{
				var delivery = new MessageDelivery()
				{
					bodySize = bodySize,
					properties = properties,
					routingKey = routingKey,
					deliveryTag = deliveryTag,
					redelivered = redelivered
				};

				var mode = consumer.Mode;
				var cb = consumer.Callback;

				if (mode == ConsumeMode.SingleThreaded)
				{
					// run with scissors
					delivery.stream = stream;

					// upon return it's assumed the user has consumed from the stream and is done with it
					await cb(delivery);

					this.Return(properties);
				}
				else 
				{
					// parallel mode. it cannot hold the frame handler, so we copy the buffer (yuck) and more forward

					// since we dont have any control on how the user 
					// will deal with the buffer we cant even re-use/use a pool, etc :-(

					// Idea: split Ringbuffer consumers, create reader barrier. once they are all done, 
					// move the read pos forward. Shouldnt be too hard to implement and 
					// avoids the new buffer + GC and keeps the api Stream based consistently

					if (mode == ConsumeMode.ParallelWithBufferCopy)
					{
						var bufferCopy = BufferUtil.Copy(stream as RingBufferStreamAdapter, (int)bodySize);
						var memStream = new MemoryStream(bufferCopy, writable: false);
						delivery.stream = memStream;
					}
					else if (mode == ConsumeMode.ParallelWithReadBarrier)
					{
						var readBarrier = new RingBufferStreamReadBarrier(stream as RingBufferStreamAdapter, delivery.bodySize);
						delivery.stream = readBarrier;
					}

#pragma warning disable 4014
					Task.Factory.StartNew(() =>
#pragma warning restore 4014
					{
						try
						{
							using (delivery.stream)
							{
								cb(delivery);
							}
						}
						catch (Exception e)
						{
							Console.WriteLine("From threadpool " + e);
						}
						finally
						{
							this.Return(properties);
						}
					}, TaskCreationOptions.PreferFairness);

// Fingers crossed the threadpool is large enough
//					ThreadPool.UnsafeQueueUserWorkItem((param) =>
//					{
//						try
//						{
//							using (delivery.stream)
//							{
//								cb(delivery);
//							}
//						}
//						catch (Exception e)
//						{
//							Console.WriteLine("From threadpool " + e);
//						}
//						finally
//						{
//							this.Return(properties);
//						}
//					}, null);
				}
			}
			else
			{
				// received msg but nobody was subscribed to get it (?) TODO: log it at least
			}
		}

		internal void ProcessAcks(ulong deliveryTags, bool multiple)
		{
			if (_confirmationKeeper != null)
			{
				_confirmationKeeper.Confirm(deliveryTags, multiple, requeue: false, isAck: true);
			}
		}

		internal void ProcessNAcks(ulong deliveryTags, bool multiple, bool requeue)
		{
			if (_confirmationKeeper != null)
			{
				_confirmationKeeper.Confirm(deliveryTags, multiple, requeue, isAck: false);
			}
		}

		internal void HandleChannelFlow(bool isActive)
		{
			if (isActive)
			{

			}
			else
			{

			}
		}

		internal async Task DispatchBasicReturn(ushort replyCode, string replyText, string exchange, string routingKey, 
												int bodySize, BasicProperties properties, Stream stream)
		{
			var ev = this.MessageUndeliveredHandler;

			// var consumed = 0;

			if (ev != null)
			{
				var inst = new UndeliveredMessage()
				{
					bodySize = bodySize,
					stream = stream,
					properties = properties,
					routingKey = routingKey,
					replyCode = replyCode,
					replyText = replyText,
					exchange = exchange
				};

				await ev(inst);
			}
		}

		internal Task EnableConfirmation(int maxunconfirmedMessages)
		{
			if (_confirmationKeeper != null) throw new Exception("Already set");
		
			_confirmationKeeper = new MessagesPendingConfirmationKeeper(maxunconfirmedMessages, _cancellationToken);

			return _io.__SendConfirmSelect(noWait: false);
		}

		internal Task Open()
		{
			return _io.Open();
		}

		internal void GenericRecycler<T>(T item, ObjectPool<T> pool) where T : class
		{
			pool.PutObject(item);
		}

		class BasicConsumerSubscriptionInfo
		{
			public ConsumeMode Mode;
			public Func<MessageDelivery, Task> Callback;
		}
	}
}
