using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tacmind.Networking
{
	public abstract class NetworkEntity : INetworkEntity
	{

		public bool IsMine { get { return OwnerId == NetworkManager.LocalId; } }


		public ushort OwnerId { get; set; }

		public ushort EntityId { get; set; }


		public abstract void OnReceive(NetworkStream ns);

		public abstract void OnSend(NetworkStream ns, ushort targetId);

		public ushort GetEntityId()
		{
			return EntityId;
		}

		public void SetEntityId(ushort id)
		{
			EntityId = id;
		}

		public ushort GetOwnerId()
		{
			return OwnerId;
		}

		public void OnSendEnd()
		{

		}
	}
}
