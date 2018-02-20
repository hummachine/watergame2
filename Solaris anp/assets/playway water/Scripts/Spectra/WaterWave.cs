using UnityEngine;

namespace PlayWay.Water
{
	public struct WaterWave : System.IComparable<WaterWave>
	{
		public readonly ushort u, v;
		public readonly float kx, kz;
		public readonly float nkx, nky;
		public readonly float w;
		public readonly byte scaleIndex;
		public readonly float dotOffset;
		
		public float amplitude;
		public float cpuPriority;         // TODO: pack in ushort
		public float offset;

		public WaterWave(byte scaleIndex, float offsetX, float offsetZ, ushort u, ushort v, float kx, float kz, float k, float w, float amplitude, float cpuPriority)
		{
			this.scaleIndex = scaleIndex;
			this.dotOffset = offsetX * kx + offsetZ * kz;// + 0.0329f;
			this.u = u;
			this.v = v;
			this.kx = kx;
			this.kz = kz;
			this.nkx = k != 0 ? kx / k : 0.707107f;
			this.nky = k != 0 ? kz / k : 0.707107f;
            this.amplitude = 2.0f * amplitude;
			this.offset = 0.0f;
			this.w = w;
			this.cpuPriority = (amplitude >= 0 ? amplitude : -amplitude);
		}

		public float k
		{
			get { return Mathf.Sqrt(kx * kx + kz * kz); }
		}

		public void UpdateSpectralValues(Vector3[][,] spectrum, Vector2 windDirection, float directionalityInv, int resolution, float horizontalScale)
		{
			var s = spectrum[scaleIndex][u, v];

			float dp = windDirection.x * nkx + windDirection.y * nky;
			float phi = Mathf.Acos(dp * 0.999f);
			float scale = Mathf.Sqrt(1.0f + s.z * Mathf.Cos(2.0f * phi));
			if(dp < 0.0f) scale *= directionalityInv;

			float sx = s.x * scale;
			float sy = s.y * scale;

			amplitude = 2.0f * Mathf.Sqrt(sx * sx + sy * sy);
			offset = Mathf.Atan2(Mathf.Abs(sx), Mathf.Abs(sy));

			if(sy > 0.0f)
			{
				amplitude = -amplitude;
				offset = -offset;
			}

			if(sx < 0.0f) offset = -offset;

			offset += dotOffset;
			cpuPriority = (amplitude >= 0 ? amplitude : -amplitude);
		}

		/// <summary>
		/// Computes full wave displacement at a point.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="z"></param>
		/// <param name="t"></param>
		/// <returns></returns>
		public Vector3 GetDisplacementAt(float x, float z, float t)
		{
			float dot = kx * x + kz * z;

			float s, c;
			FastMath.SinCos2048(dot + t * w + offset, out s, out c);

			c *= amplitude;

			return new Vector3(nkx * c, s * amplitude, nky * c);
		}

		// (kx, kz, w) * (x, z, t)
		public Vector2 GetRawHorizontalDisplacementAt(float x, float z, float t)
		{
			float dot = kx * x + kz * z;
			float c = amplitude * Mathf.Cos(dot + t * w + offset);

			return new Vector2(nkx * c, nky * c);
		}

		public void GetForceAndHeightAt(float x, float z, float t, ref Vector4 result)
		{
			float dot = kx * x + kz * z;

			//FastMath.SinCos2048(dot + t * w + offset, out s, out c);		// inlined below
			int icx = ((int)((dot + t * w + offset) * 325.949f) & 2047);
			float s = FastMath.sines[icx];
			float c = FastMath.cosines[icx];

			float sa = amplitude * s;
			float ca = amplitude * c;

			result.x += nkx * sa;
			result.z += nky * sa;
			result.y += ca;
			result.w += sa;
		}

		public float GetHeightAt(float x, float z, float t)
		{
			float dot = kx * x + kz * z;
			return amplitude * Mathf.Sin(dot + t * w + offset);
		}

		public int CompareTo(WaterWave other)
		{
			return other.cpuPriority.CompareTo(cpuPriority);
		}

		private float DisplacementFunc(float r, float precomp)
		{
			return amplitude * Mathf.Cos(k * r + precomp) + r;
		}

		private float DisplacementFuncDerivative(float r, float precomp)
		{
			return 1.0f - k * amplitude * Mathf.Sin(k * r + precomp);
		}
	}
}
