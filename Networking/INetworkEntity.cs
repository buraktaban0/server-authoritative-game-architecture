using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tacmind.Networking
{
	public interface INetworkEntity
	{


		void OnSend(NetworkStream ns, ushort targetId);

		void OnSendEnd();

		void OnReceive(NetworkStream ns);

		void SetEntityId(ushort id);

		ushort GetEntityId();

		ushort GetOwnerId();

	}
}
