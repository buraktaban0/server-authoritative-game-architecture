using Tacmind.Inputs;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Tacmind.Networking
{
	public class NetworkPlayer : NetworkTarget, INetworkObject
	{

		public string Name { get; set; }


		

		public NetworkPlayer(IPEndPoint remoteEP, ushort id) : base(remoteEP, "", id)
		{
		}
		/*
		public override void EnqueuePacket(NetworkStream ns)
		{
			if (ns.dataType == NetworkStream.DataType.UserCommand)
			{
				EnqueueCommands(ns);
				return;
			}

			//ReceiveQueue.Enqueue(ns);
		}

		private void EnqueueCommands(NetworkStream ns)
		{
			int count = ns.ReadByte();
			for (int i = 0; i < count; i += 1)
			{
				UserCommand cmd = new UserCommand();
				cmd.ReadFrom(ns);
				Commands.Enqueue(cmd);
			}
		}*/

		public void ReadFrom(NetworkStream ns)
		{
			Id = ns.ReadUShort();
			Name = ns.ReadString();
			RemoteEP = NetworkUtility.ParseIPEndPoint(ns.ReadString());
		}

		public void WriteTo(NetworkStream ns)
		{
			ns.WriteUShort(Id);
			ns.WriteString(Name);
			ns.WriteString(RemoteEP.ToString());
		}
	}
}
