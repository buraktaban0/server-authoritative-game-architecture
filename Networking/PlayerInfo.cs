using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tacmind.Networking
{
	public struct PlayerInfo : INetworkObject
	{
		public ushort Id { get; set; }

		public string Name { get; set; }

		public void ReadFrom(NetworkStream ns)
		{
			Id = ns.ReadUShort();
			Name = ns.ReadString();
		}

		public void WriteTo(NetworkStream ns)
		{
			ns.WriteUShort(Id);
			ns.WriteString(Name);
		}
	}
}
