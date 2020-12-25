using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Tacmind.Networking
{
	public class TimeSynchronizer
	{
		public float ServerTimestep { get; set; }

		private int serverTicks = 0;

		private float startTime;
		private float expectedTime;
		private float integrator = 0f;
		private float drift = 0f;

		private float lastUpdateTime;

		public TimeSynchronizer(float serverTimestep)
		{
			ServerTimestep = serverTimestep;

			startTime = GetNow();

			lastUpdateTime = startTime;

			expectedTime = GetNow() + ServerTimestep;
		}


		public void OnServerUpdate()
		{
			float now = GetNow();
			float timeSinceLast = now - lastUpdateTime;
			lastUpdateTime = now;

			int ticks = (int)(timeSinceLast / ServerTimestep + 0.5f);

			serverTicks += ticks;

			float diff = (expectedTime - now) % ServerTimestep;

			integrator = integrator * 0.9f + diff;

			float adjustment = Mathf.Clamp(integrator * 0.01f, -0.001f, 0.001f);
			drift += adjustment;

			expectedTime += ServerTimestep * ticks;
			
		}

		public float GetServerTime(float delta)
		{
			return serverTicks * ServerTimestep; // - delta;
		}

		public float TimeSinceStart()
		{
			return GetNow() - startTime;
		}

		public float GetNow()
		{
			return Time.time + drift;
		}


	}
}
