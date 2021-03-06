namespace RabbitMqNext.Internals.RingBuffer
{
	/// <summary>
	/// Measures how much has been read since started, 
	/// limited to small amounts (wont support overflowing over and over)
	/// </summary>
	/// TODO: broken when used with ReadGates, needs fix
	internal struct RingBufferPositionMarker
	{
		private readonly ByteRingBuffer _ringBuffer;
		private uint _start;

		public RingBufferPositionMarker(ByteRingBuffer ringBuffer)
		{
			_ringBuffer = ringBuffer;
			_start = _ringBuffer.GlobalReadPos;
		}

		public uint LengthRead
		{
			get
			{
				var curReadPos = _ringBuffer.GlobalReadPos;
				if (curReadPos < _start) // overflowed
				{
					return (uint.MaxValue - _start) + curReadPos;
				}
				return curReadPos - _start;
			}
		}
	}
}