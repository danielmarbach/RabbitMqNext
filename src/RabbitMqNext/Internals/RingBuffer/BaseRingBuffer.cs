﻿namespace RabbitMqNext.Internals.RingBuffer
{
	using System;
	using System.Runtime.CompilerServices;
	using System.Runtime.InteropServices;
	using System.Threading;


	internal abstract class BaseRingBuffer
	{
		protected readonly uint _bufferSize;

		[StructLayout(LayoutKind.Explicit, Size = 64)]
		internal struct ReadR
		{
			[FieldOffset(0)] 
			internal volatile uint _readPosition;
		}
		[StructLayout(LayoutKind.Explicit, Size = 64)]
		internal struct WriteW
		{
			[FieldOffset(0)]
			internal volatile uint _writePosition;
		}

		internal ReadR _readP;
		internal WriteW _writeP;

		protected readonly CancellationToken _cancellationToken;
		protected readonly WaitingStrategy _waitingStrategy;
		
		// max gates = 256
		const int MaxGates = 32; 
		private readonly ReadingGate[] _gates = new ReadingGate[MaxGates];
		private volatile int _gateState = 0; // 11111111 11111111 11111111 11111111

		// adds to the current position
		internal ReadingGate AddReadingGate(uint length)
		{
			if (_gateState == -1) throw new Exception("Max gates reached");

			var gate = new ReadingGate() { gpos = _readP._readPosition, length = length };

			AtomicSecureIndexPosAndStore(gate);

			return gate;
		}

		internal void RemoveReadingGate(ReadingGate gate)
		{
			if (gate.inEffect == false) return;
			gate.inEffect = false;
			AtomicRemoveAtIndex(gate.index);
			// if waiting for write coz of gate, given them another chance
			_waitingStrategy.SignalReadDone();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal uint? GetMinReadingGate()
		{
			uint minGatePos = uint.MaxValue;

			int oldGateState = 0;
			do
			{
				oldGateState = _gateState;
				if (oldGateState == 0) return null;

				for (int i = 0; i < MaxGates; i++)
				{
					if ((oldGateState & (1 << i)) != 0)
					{
						var el = _gates[i];
						if (el == null) // race
							continue;
						if (el.inEffect) // otherwise ignored
						{
							if (minGatePos > el.gpos)
								minGatePos = el.gpos;
						}
					}
				}
				// if it changed in the meantime, we need to recalculate
			} while (oldGateState != _gateState); 

			return minGatePos;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal int InternalGetReadyToReadEntries(int desiredCount, out int available, ReadingGate fromGate = null)
		{
			uint bufferSize = _bufferSize;

			uint writeCursor = _writeP._writePosition; // volative read
			uint readCursor = _readP._readPosition;   // volative read

			uint writePos = writeCursor & (bufferSize - 1); // (writeCursor % _bufferSize);
			uint readPos = readCursor & (bufferSize - 1);   // (readCursor % _bufferSize);

			uint entriesFree;

			if (fromGate != null)
			{
				// Console.WriteLine("Reading from gate. Real readpos " + readPos + " replaced by " + fromGate.pos);
				readPos = fromGate.gpos & (bufferSize - 1);
				// entriesFree = fromGate.length;
				desiredCount = Math.Min(desiredCount, (int) fromGate.length);
			}

			// else
			{
				var writeHasWrapped = writePos < readPos;

				if (writeHasWrapped) // so everything ahead of readpos is available
				{
					entriesFree = bufferSize - readPos;
				}
				else
				{
					entriesFree = writePos - readPos;
				}
			}

#if DEBUG
			if (entriesFree > _bufferSize)
			{
				var msg = "Assert failed read: " + entriesFree + " must be less or equal to " + (BufferSize);
				System.Diagnostics.Debug.WriteLine(msg);
				throw new Exception(msg);
			}
#endif

//			uint available;
//			if (fromGate != null)
//			{
//				available = Math.Min(entriesFree, (uint) desiredCount);
//			}
//			else
			{
				available = (int) Math.Min(entriesFree, (uint)desiredCount);
			}

			// return available;
			// return new AvailableAndPos() { available = (int)available, position = (int)readPos };
			return (int) readPos;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal int InternalGetReadyToWriteEntries(int desiredCount, out int available)
		{
			var buffersize = _bufferSize;

			uint writeCursor = _writeP._writePosition; // volative read
			uint readCursor = _readP._readPosition;   // volative read

			uint writePos = writeCursor & (buffersize - 1);
			uint readPos = readCursor & (buffersize - 1);
#if DEBUG
			var originalreadPos = readPos;
#endif

			uint entriesFree = 0;

			// damn gates. 
//			var minGate = GetMinReadingGate();
//			if (minGate.HasValue) 
//			{
//				// get min gate index, which becomes essentially the barrier to continue to write
//				// what we do is to hide from this operation the REAL readpos
//
//				// Console.WriteLine("Got gate in place. real readPos " + readPos + " becomes " + minGate.Value);
//
//				readPos = minGate.Value & (buffersize - 1); // now the write cannot move forward
//			} 

			var writeWrapped = readPos > writePos;

			if (writeWrapped)
			{
				var availableTilWrap = readPos - writePos - 1;
				entriesFree = availableTilWrap;
			}
			else
			{
				if (readPos == 0)
					entriesFree = buffersize - writePos - 1;
				else
					entriesFree = buffersize - writePos;
			}

#if DEBUG
			if (writeWrapped)
			{
				if (!(entriesFree <= _bufferSize - 1))
				{
					var msg = "Assert write1 failed: " + entriesFree + " must be less or equal to " + (BufferSize - 1) +
						" originalreadPos " + originalreadPos + " readpos " + readPos + " write " + writePos +
						" G w " + _writeP._writePosition + " G r " + _readP._readPosition;
					System.Diagnostics.Debug.WriteLine(msg);
					throw new Exception(msg);
				}
			}
			else
			{
				if (!(entriesFree <= _bufferSize))
				{
					var msg = "Assert write2 failed: " + entriesFree + " must be less or equal to " + (BufferSize);
					System.Diagnostics.Debug.WriteLine(msg);
					throw new Exception(msg);
				}
			}
#endif

			available = (int) Math.Min(entriesFree, (uint)desiredCount);
			// return available;
			// return new AvailableAndPos() { available = (int)available, position = (int)writePos };
			return (int) writePos;
		}

		protected BaseRingBuffer(CancellationToken cancellationToken, int bufferSize, WaitingStrategy waitingStrategy)
		{
			if (bufferSize <= 0) throw new ArgumentOutOfRangeException("bufferSize");
			if (!Utils.IsPowerOfTwo(bufferSize)) throw new ArgumentException("bufferSize must be multiple of 2", "bufferSize");

			_cancellationToken = cancellationToken;
			_waitingStrategy = waitingStrategy;
			_bufferSize = (uint)bufferSize;
		}

		public int BufferSize
		{
			get { return (int)_bufferSize; }
		}

		public bool HasUnreadContent
		{
			// two volatives reads
			get { return _writeP._writePosition != _readP._readPosition; }
		}

//		internal struct AvailableAndPos
//		{
//			public int available, position;
//		}

		private void AtomicSecureIndexPosAndStore(ReadingGate gate)
		{
			while (true)
			{
				var curState = _gateState; // vol read
				var emptyIndex = -1;

				// find empty spot
				for (var i = 0; i < MaxGates; i++)
				{
					int mask = 1 << i;
					if ((curState & mask) == 0)
					{
						emptyIndex = i; 
						break;
					}
				}

				if (emptyIndex == -1) continue; // try again from the beginning

				int newState = curState | (1 << emptyIndex);

				gate.index = emptyIndex;

#pragma warning disable 420
				if (Interlocked.CompareExchange(ref _gateState, newState, curState) != curState)
#pragma warning restore 420
				{
					// state was changed. try again
					continue;
				}

				_gates[emptyIndex] = gate; // race between changing the state and saving to array.
				break;
			}
		}

		private void AtomicRemoveAtIndex(int index)
		{
			while (true)
			{
				var curState = _gateState; // vol read
				var mask = ~(1 << index);
				int newState = curState & mask;

#pragma warning disable 420
				if (Interlocked.CompareExchange(ref _gateState, newState, curState) != curState)
#pragma warning restore 420
				{
					// state was changed. try again
					continue;
				}

				break;
			}
		}

		// For unit testing only

		internal uint GlobalReadPos { get { return _readP._readPosition; } }
		internal uint GlobalWritePos { get { return _writeP._writePosition; } }
		internal uint LocalReadPos { get { return _readP._readPosition % _bufferSize; } }
		internal uint LocalWritePos { get { return _writeP._writePosition % _bufferSize; } }

	}
}