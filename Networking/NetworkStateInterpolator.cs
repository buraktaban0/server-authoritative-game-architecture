using Tacmind.Collections;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Tacmind.ResourceManagement;
using Tacmind.Logging;
using Tacmind.Characters;

namespace Tacmind.Networking
{
	public abstract class NetworkStateInterpolator<T> where T : NetworkState, new()
	{
		public T LastState { get { return States.Last.v1; } }

		public BoundedList<Tuple2<T, float>> States { get; private set; }

		public float Delay { get; set; }
		public float ServerTimestep { get; set; }

		public bool IsAvailable { get { return States.Count >= States.Capacity; } }

		private TimeSynchronizer timeSync;


		private AutoPool<T> statePool;

		public NetworkStateInterpolator(float serverTimestep, float delay = -1)
		{
			statePool = new AutoPool<T>(512);

			if (delay < 0)
			{
				delay = 1.5f * serverTimestep;
			}

			this.Delay = delay;


			this.ServerTimestep = serverTimestep;

			int capacity = (int)(delay / serverTimestep) + 2;

			States = new BoundedList<Tuple2<T, float>>(capacity);

			timeSync = new TimeSynchronizer(serverTimestep);
		}

		public void Add(T state, float time)
		{
			//timeSync.OnServerUpdate();


			//float serverTime = Time.time; // timeSync.GetServerTime(delay);


			States.Add(new Tuple2<T, float>(state, time));
		}


		public abstract void Lerp(T from, T to, ref T into, float a);


		public T Get()
		{
			float time = NetworkUtility.Time - Delay; // Time.time - Delay; // timeSync.TimeSinceStart() - Delay;

			T current = new T(); // statePool.Get();

			Tuple2<T, float> last = States.Last;
			if (last.v2 < time)
			{
				Tuple2<T, float> second = States[States.Count - 2];
				float a = GetLerpParam(second.v2, last.v2, time);
				Lerp(second.v1, last.v1, ref current, a);
				Log.WriteErrorLine("OVERFLOW!!  " + a);
				AddToDelay(0.001f);
				return current;
			}

			for (int i = States.Count - 2; i >= 0; i -= 1)
			{
				Tuple2<T, float> tupleOld = States[i];
				if (tupleOld.v2 < time)
				{
					Tuple2<T, float> tupleNew = States[i + 1];
					float a = GetLerpParam(tupleOld.v2, tupleNew.v2, time);
					Lerp(tupleOld.v1, tupleNew.v1, ref current, a);
					AddToDelay(-0.000001f);
					return current;
				}
			}

			AddToDelay(-0.0005f);

			return States[0].v1;


		}

		private void AddToDelay(float delta)
		{
			Delay = Mathf.Clamp(Delay + delta, ServerTimestep, ServerTimestep * 3f);
		}


		private float GetLerpParam(float min, float max, float val)
		{
			return Mathf.Clamp((val - min) / (max - min), -1f, 2f);
		}

	}
}
