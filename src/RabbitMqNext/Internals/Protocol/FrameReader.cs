namespace RabbitMqNext.Internals
{
	using System;
	using System.Threading;
	using System.Threading.Tasks;

	internal partial class FrameReader
	{
		private readonly InternalBigEndianReader _reader;
		private readonly AmqpPrimitivesReader _amqpReader;
		private readonly IFrameProcessor _frameProcessor;

		public FrameReader(InternalBigEndianReader reader, 
						   AmqpPrimitivesReader amqpReader,
						   IFrameProcessor frameProcessor)
		{
			_reader = reader;
			_amqpReader = amqpReader;
			_frameProcessor = frameProcessor;
		}

		public void ReadAndDispatch()
		{
			try
			{
				byte frameType = _reader.ReadByte();
//				Console.WriteLine("Frame type " + frameType);

				if (frameType == 'A')
				{
					// wtf
					Console.WriteLine("Meh, protocol header received for some reason. darn it!");
				}

				ushort channel = _reader.ReadUInt16();
				int payloadLength = _reader.ReadInt32();

//				Console.WriteLine("> Incoming Frame (" + frameType + ") for channel [" + channel + "]  payload size: " + payloadLength);

				// needs special case for heartbeat, flow, etc.. 
				// since they are not replies to methods we sent and alter the client's behavior

				ushort classId = 0;
				ushort methodId = 0;

				if (frameType == AmqpConstants.FrameMethod)
				{
					classId = _reader.ReadUInt16();
					methodId = _reader.ReadUInt16();

					var classMethodId = classId << 16 | methodId;

//					Console.WriteLine("> Incoming Method: class " + classId + " method " + methodId + " classMethodId " + classMethodId);

					_frameProcessor.DispatchMethod(channel, classMethodId);
				}
				else if (frameType == AmqpConstants.FrameHeartbeat)
				{
					Console.WriteLine("received FrameHeartbeat");
				}

				byte frameEndMarker = _reader.ReadByte();
				if (frameEndMarker != AmqpConstants.FrameEnd)
				{
					throw new Exception("Expecting frame end, but found " + frameEndMarker);
				}
			}
			catch (ThreadAbortException)
			{
				// no-op
			}
			catch (Exception ex)
			{
				Console.WriteLine("Frame Reader error: " + ex);
				throw;
			}
		}
	}
}