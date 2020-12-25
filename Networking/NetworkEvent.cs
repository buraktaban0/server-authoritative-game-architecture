using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tacmind.ResourceManagement;

namespace Tacmind.Networking
{
	public enum NetworkEventType : byte
	{
		None,
		Connect,
		Matchmaking,
		LoadMap,
		Instantiate,
		Destroy,
		Fire,
		Hit,
		TurretKill,
		TicketUpdate,
		StatsUpdate,
		GameTimeUpdate,
		GameEnd,
		MatchFailed,
		PlayersReady,
		PlayerInfoFull,
		PlayerInfoPartial,
		ShopRequestResult
	}

	public class NetworkEvent
	{
		private static AutoPool<NetworkStream> streamPool = new AutoPool<NetworkStream>(1024 * 4);

		public static NetworkStream Create(NetworkEventType type)
		{
			NetworkStream stream = new NetworkStream(256); //streamPool.Get(); 

			stream.reliability = NetworkStream.Reliability.Reliable;
			stream.dataType = NetworkStream.DataType.Event;
			stream.WriteByte((byte)type);


			return stream;
		}

	}
}
