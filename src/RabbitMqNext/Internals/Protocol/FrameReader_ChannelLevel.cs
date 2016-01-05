namespace RabbitMqNext.Internals
{
	using System;
	using System.IO;
	using System.Threading.Tasks;

	internal partial class FrameReader
	{
		public void Read_BasicQosOk(Action continuation)
		{
			Console.WriteLine("< BasicQosOk  ");

			continuation();
		}

		public void Read_ExchangeDeclareOk(Action continuation)
		{
			Console.WriteLine("< ExchangeDeclareOk");

			continuation();
		}

		public void Read_QueueDeclareOk(Action<string, uint, uint> continuation)
		{
			Console.WriteLine("< QueueDeclareOk");

			var queue = _amqpReader.ReadShortStr();
			var messageCount = _amqpReader.ReadLong();
			var consumerCount = _amqpReader.ReadLong();

			continuation(queue, messageCount, consumerCount);
		}

		public void Read_QueueBindOk(Action continuation)
		{
			Console.WriteLine("< QueueBindOk");

			continuation();
		}

		public async Task Read_Channel_Close2(Action<ushort, string, ushort, ushort> continuation)
		{
			var replyCode = _amqpReader.ReadShort();
			var replyText = _amqpReader.ReadShortStr();
			var classId = _amqpReader.ReadShort();
			var methodId = _amqpReader.ReadShort();

			Console.WriteLine("< channel close coz  " + replyText + " in class  " + classId + " methodif " + methodId);

			continuation(replyCode, replyText, classId, methodId);
		}

		public async Task Read_BasicDelivery(Action<string, ulong, bool, string, string, long, BasicProperties, Stream> continuation)
		{
			var consumerTag = _amqpReader.ReadShortStr();
			var deliveryTag = _amqpReader.ReadULong();
			var redelivered = _amqpReader.ReadBits() != 0;
			var exchange = _amqpReader.ReadShortStr();
			var routingKey = _amqpReader.ReadShortStr();

			var frameEndMarker = _amqpReader.ReadOctet();
			if (frameEndMarker != AmqpConstants.FrameEnd) throw new Exception("Expecting frameend!");

			// Frame Header / Content header

			var frameHeaderStart = _amqpReader.ReadOctet();
			if (frameHeaderStart != AmqpConstants.FrameHeader) throw new Exception("Expecting Frame Header");

			ushort channel = _reader.ReadUInt16();
			int payloadLength = _reader.ReadInt32();
			var classId = _reader.ReadUInt16();
			var weight = _reader.ReadUInt16();
			var bodySize = (long) _reader.ReadUInt64();

			var properties = await ReadRestOfContentHeader();

			// Frame Body(s)

			// Support just single body at this moment.

			frameHeaderStart = _reader.ReadByte();
			if (frameHeaderStart != AmqpConstants.FrameBody) throw new Exception("Expecting Frame Body");

			channel = _reader.ReadUInt16();
			var length = _reader.ReadUInt32();

			// Pending Frame end

			if (length == bodySize)
			{
				continuation(consumerTag, deliveryTag,
					redelivered, exchange,
					routingKey, length, properties, (Stream) _reader._ringBufferStream);
			}
			else
			{
				throw new NotSupportedException("Multi body not supported yet. Total body size is " + bodySize + " and first body is " + length + " bytes");
			}
		}

//		private async Task PositionAtBodyStream()
//		{
//			
//		}

		private async Task<BasicProperties> ReadRestOfContentHeader()
		{
			var presence = _reader.ReadUInt16();

			BasicProperties properties;

			if (presence == 0) // no header content
			{
				properties = BasicProperties.Empty;
			}
			else
			{
				properties = new BasicProperties {_presenceSWord = presence};
				if (properties.IsContentTypePresent) { properties.ContentType = _amqpReader.ReadShortStr(); }
				if (properties.IsContentEncodingPresent) { properties.ContentEncoding = _amqpReader.ReadShortStr(); }
				if (properties.IsHeadersPresent) { properties.Headers = await _amqpReader.ReadTable(); }
				if (properties.IsDeliveryModePresent) { properties.DeliveryMode = _amqpReader.ReadOctet(); }
				if (properties.IsPriorityPresent) { properties.Priority = _amqpReader.ReadOctet(); }
				if (properties.IsCorrelationIdPresent) { properties.CorrelationId = _amqpReader.ReadShortStr(); }
				if (properties.IsReplyToPresent) { properties.ReplyTo = _amqpReader.ReadShortStr(); }
				if (properties.IsExpirationPresent) { properties.Expiration = _amqpReader.ReadShortStr(); }
				if (properties.IsMessageIdPresent) { properties.MessageId = _amqpReader.ReadShortStr(); }
				if (properties.IsTimestampPresent) { properties.Timestamp = await _amqpReader.ReadTimestamp(); }
				if (properties.IsTypePresent) { properties.Type = _amqpReader.ReadShortStr(); }
				if (properties.IsUserIdPresent) { properties.UserId = _amqpReader.ReadShortStr(); }
				if (properties.IsAppIdPresent) { properties.AppId = _amqpReader.ReadShortStr(); }
				if (properties.IsClusterIdPresent) { properties.ClusterId = _amqpReader.ReadShortStr(); }
			}

			int frameEndMarker = _reader.ReadByte();
			if (frameEndMarker != AmqpConstants.FrameEnd) throw new Exception("Expecting frameend!");

			return properties;
		}

		public void Read_BasicConsumeOk(Action<string> continuation)
		{
			var consumerTag = _amqpReader.ReadShortStr();

			Console.WriteLine("< BasicConsumeOk ");

			continuation(consumerTag);
		}

		public void Read_Channel_CloseOk(Action continuation)
		{
			Console.WriteLine("< channel close OK coz ");

			continuation();
		}
	}
}