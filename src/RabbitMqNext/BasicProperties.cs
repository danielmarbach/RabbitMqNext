namespace RabbitMqNext
{
	using System;
	using System.Collections.Generic;
	using System.Runtime.CompilerServices;
	using System.Text;

	public class BasicProperties
	{
		internal static readonly BasicProperties Empty = new BasicProperties(isFrozen: true);

		private readonly bool _isFrozen;

		// 0x01 is reserved for continuation flag. 
		private const ushort ContentTypePresence = 1 << 15;
		private const ushort ContentEncodingPresence = 1 << 14;
		private const ushort HeadersPresence = 1 << 13;
		private const ushort DeliveryModePresence = 1 << 12;
		private const ushort PriorityPresence = 1 << 11;
		private const ushort CorrelationIdPresence = 1 << 10;
		private const ushort ReplyToPresence = 1 << 9;
		private const ushort ExpirationPresence = 1 << 8;
		private const ushort MessageIdPresence = 1 << 7;
		private const ushort TimestampPresence = 1 << 6;
		private const ushort TypePresence = 1 << 5;
		private const ushort UserIdPresence = 1 << 4;
		private const ushort AppIdPresence = 1 << 3;
		private const ushort ClusterIdPresence = 1 << 2;

		internal ushort _presenceSWord = 0;

		private IDictionary<string, object> _headers;
		private AmqpTimestamp _timestamp;
		private byte _deliveryMode;
		private byte _priority;
		private string _contentType;
		private string _contentEncoding;
		private string _correlationId;
		private string _replyTo;
		private string _expiration;
		private string _messageId;
		private string _type;
		private string _userId;
		private string _appId;
		private string _clusterId;

		public BasicProperties() : this(isFrozen: false)
		{
		}

		internal BasicProperties(bool isFrozen)
		{
			_isFrozen = isFrozen;
		}

		public bool IsContentTypePresent
		{
			get { return (_presenceSWord & ContentTypePresence) != 0; }
			internal set { _presenceSWord |= value ? ContentTypePresence : (ushort)0; }
		}

		public bool IsContentEncodingPresent
		{
			get { return (_presenceSWord & ContentEncodingPresence) != 0; }
			internal set { _presenceSWord |= value ? ContentEncodingPresence : (ushort)0; }
		}

		public bool IsHeadersPresent
		{
			get { return (_presenceSWord & HeadersPresence) != 0; }
			internal set { _presenceSWord |= value ? HeadersPresence : (ushort)0; }
		}

		public bool IsDeliveryModePresent
		{
			get { return (_presenceSWord & DeliveryModePresence) != 0; }
			internal set { _presenceSWord |= value ? DeliveryModePresence : (ushort)0; }
		}

		public bool IsPriorityPresent
		{
			get { return (_presenceSWord & PriorityPresence) != 0; }
			internal set { _presenceSWord |= value ? PriorityPresence : (ushort)0; }
		}

		public bool IsCorrelationIdPresent
		{
			get { return (_presenceSWord & CorrelationIdPresence) != 0; }
			internal set { _presenceSWord |= value ? CorrelationIdPresence : (ushort)0; }
		}

		public bool IsReplyToPresent
		{
			get { return (_presenceSWord & ReplyToPresence) != 0; }
			internal set { _presenceSWord |= value ? ReplyToPresence : (ushort)0; }
		}

		public bool IsExpirationPresent
		{
			get { return (_presenceSWord & ExpirationPresence) != 0; }
			internal set { _presenceSWord |= value ? ExpirationPresence : (ushort)0; }
		}

		public bool IsMessageIdPresent
		{
			get { return (_presenceSWord & MessageIdPresence) != 0; }
			internal set { _presenceSWord |= value ? MessageIdPresence : (ushort)0; }
		}

		public bool IsTimestampPresent
		{
			get { return (_presenceSWord & TimestampPresence) != 0; }
			internal set { _presenceSWord |= value ? TimestampPresence : (ushort)0; }
		}

		public bool IsTypePresent
		{
			get { return (_presenceSWord & TypePresence) != 0; }
			internal set { _presenceSWord |= value ? TypePresence : (ushort)0; }
		}

		public bool IsUserIdPresent
		{
			get { return (_presenceSWord & UserIdPresence) != 0; }
			internal set { _presenceSWord |= value ? UserIdPresence : (ushort)0; }
		}

		public bool IsAppIdPresent
		{
			get { return (_presenceSWord & AppIdPresence) != 0; }
			internal set { _presenceSWord |= value ? AppIdPresence : (ushort)0; }
		}

		public bool IsClusterIdPresent
		{
			get { return (_presenceSWord & ClusterIdPresence) != 0; }
			internal set { _presenceSWord |= value ? ClusterIdPresence : (ushort)0; }
		}

		public string ContentType
		{
			get { return _contentType; }
			set
			{
				ThrowIfFrozen();
				IsContentTypePresent = !string.IsNullOrEmpty(value);
				_contentType = value;
			}
		}

		public string ContentEncoding
		{
			get { return _contentEncoding; }
			set
			{
				ThrowIfFrozen();
				IsContentEncodingPresent = !string.IsNullOrEmpty(value);
				_contentEncoding = value;
			}
		}

		public string CorrelationId
		{
			get { return _correlationId; }
			set
			{
				ThrowIfFrozen();
				IsCorrelationIdPresent = !string.IsNullOrEmpty(value);
				_correlationId = value;
			}
		}

		public string ReplyTo
		{
			get { return _replyTo; }
			set
			{
				ThrowIfFrozen();
				IsReplyToPresent = !string.IsNullOrEmpty(value);
				_replyTo = value;
			}
		}

		public string Expiration
		{
			get { return _expiration; }
			set
			{
				ThrowIfFrozen();
				IsExpirationPresent = !string.IsNullOrEmpty(value);
				_expiration = value;
			}
		}

		public string MessageId
		{
			get { return _messageId; }
			set
			{
				ThrowIfFrozen();
				IsMessageIdPresent = !string.IsNullOrEmpty(value);
				_messageId = value;
			}
		}

		public string Type
		{
			get { return _type; }
			set
			{
				ThrowIfFrozen();
				IsTypePresent = !string.IsNullOrEmpty(value);
				_type = value;
			}
		}

		public string UserId
		{
			get { return _userId; }
			set
			{
				ThrowIfFrozen();
				IsUserIdPresent = !string.IsNullOrEmpty(value);
				_userId = value;
			}
		}

		public string AppId
		{
			get { return _appId; }
			set
			{
				ThrowIfFrozen();
				IsAppIdPresent = !string.IsNullOrEmpty(value);
				_appId = value;
			}
		}

		public string ClusterId
		{
			get { return _clusterId; }
			set
			{
				ThrowIfFrozen();
				IsClusterIdPresent = !string.IsNullOrEmpty(value);
				_clusterId = value;
			}
		}

		public byte DeliveryMode
		{
			get { return _deliveryMode; }
			set
			{
				ThrowIfFrozen();
				IsDeliveryModePresent = value != 0;
				_deliveryMode = value;
			}
		}

		public byte Priority
		{
			get { return _priority; }
			set
			{
				ThrowIfFrozen();
				IsPriorityPresent = value != 0;
				_priority = value;
			}
		}

		public AmqpTimestamp Timestamp
		{
			get { return _timestamp; }
			set
			{
				ThrowIfFrozen();
				IsTimestampPresent = true;
				_timestamp = value;
			}
		}

		public IDictionary<string, object> Headers
		{
			get { return _headers; }
			set
			{
				ThrowIfFrozen();
				IsHeadersPresent = value != null;
				_headers = value;
			}
		}

		internal bool IsEmpty
		{
			get
			{
				if (_isFrozen) return true;
				return _presenceSWord == 0;
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void ThrowIfFrozen()
		{
			if (_isFrozen) throw new Exception("This object is frozen so it cannot be changed");
		}

//		internal int ComputeSize()
//		{
//			return ((_deliveryMode != 0) ? 1 : 0) +
//			       ((_priority != 0) ? 1 : 0) +
//			       ((IsTimestampPresent) ? 8 : 0) +
//			       (String.IsNullOrEmpty(_contentType) ? 0 : 1 + Encoding.UTF8.GetByteCount(_contentType)) +
//			       (String.IsNullOrEmpty(_contentEncoding) ? 0 : 1 + Encoding.UTF8.GetByteCount(_contentEncoding)) +
//			       (String.IsNullOrEmpty(_correlationId) ? 0 : 1 + Encoding.UTF8.GetByteCount(_correlationId)) +
//			       (String.IsNullOrEmpty(_replyTo) ? 0 : 1 + Encoding.UTF8.GetByteCount(_replyTo)) +
//			       (String.IsNullOrEmpty(_type) ? 0 : 1 + Encoding.UTF8.GetByteCount(_type)) +
//			       (String.IsNullOrEmpty(_messageId) ? 0 : 1 + Encoding.UTF8.GetByteCount(_messageId)) +
//			       (String.IsNullOrEmpty(_expiration) ? 0 : 1 + Encoding.UTF8.GetByteCount(_expiration)) +
//			       (String.IsNullOrEmpty(_userId) ? 0 : 1 + Encoding.UTF8.GetByteCount(_userId)) +
//				   (String.IsNullOrEmpty(_appId) ? 0 : 1 + Encoding.UTF8.GetByteCount(_appId)) +
//				   (String.IsNullOrEmpty(_clusterId) ? 0 : 1 + Encoding.UTF8.GetByteCount(_clusterId)) +
//			       0; // Header!!;
//		}
	}
}