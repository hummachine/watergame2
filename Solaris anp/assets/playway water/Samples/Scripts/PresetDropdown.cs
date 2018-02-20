using UnityEngine;
using UnityEngine.UI;
using System.Collections;

namespace PlayWay.Water.Samples
{
	public class PresetDropdown : MonoBehaviour
	{
		[SerializeField]
		private Water water;

		[SerializeField]
		private WaterProfile[] profiles;

#if !UNITY_5_0 && !UNITY_5_1
		[SerializeField]
		private Dropdown dropdown;
#endif

		[SerializeField]
		private Slider progressSlider;

		private WaterProfile sourceProfile;
		private WaterProfile targetProfile;
		private float changeTime = float.NaN;

		void Start()
		{
#if !UNITY_5_0 && !UNITY_5_1
			dropdown.onValueChanged.AddListener(OnValueChanged);
#endif

			if(water.Profiles == null)
			{
				enabled = false;
				return;
			}


			StartCoroutine(CheckWeather());

			targetProfile = water.Profiles[0].profile;
		}

		public void SkipPresetTransition()
		{
			changeTime = -100.0f;
		}

		IEnumerator CheckWeather(){
			while (true) {
				//API CALL
				string weatherUrl = "http://api.openweathermap.org/data/2.5/weather?lat=17.75&lon=142.5000&appid=0a6873b7dbe4cadef95f7ea3042afb95";
				WWW weatherWWW = new WWW (weatherUrl);
				yield return weatherWWW;
				Debug.Log (weatherWWW.text);
				JSONObject tempData = new JSONObject (weatherWWW.text);

				JSONObject placeDetails = tempData ["name"];
				Debug.Log (placeDetails);

				JSONObject weatherDetails = tempData ["weather"];
				string WeatherType = weatherDetails [0] ["main"].str;
				Debug.Log (WeatherType);
				string WeatherID = weatherDetails [0] ["id"].ToString ();
				Debug.Log (WeatherID);

				JSONObject windDetails = tempData ["wind"];
				var windDetailsSpeed = windDetails ["speed"];
				var windDetailsDeg = windDetails ["deg"];
				Debug.Log ("windspeed: " + windDetailsSpeed);
				Debug.Log ("winddeg: " + windDetailsDeg);

				switch (WeatherID) {
				case "200":
				case "201":
					//200 thunderstorm with light rain     11d
					//201 thunderstorm with rain   11d
					Debug.Log ("stormy waves");
					OnValueChanged (5);
					break;
				case "202":
					//202 thunderstorm with heavy rain     11d
					Debug.Log ("stormy waves");
					OnValueChanged (6);
					break;
				case "210":
				case "211":
					//210 light thunderstorm   11d
					//211 thunderstorm     11d
					Debug.Log ("stormy waves");
					OnValueChanged (5);
					break;
				case "212":
				case "221":
					//212 heavy thunderstorm   11d
					//221 ragged thunderstorm  11d
					Debug.Log ("stormy waves");
					OnValueChanged (6);
					break;
				case "230":
				case "231":
					//230 thunderstorm with light drizzle  11d
					//231 thunderstorm with drizzle    11d
					Debug.Log ("stormy waves");
					OnValueChanged (5);
					break;
				case "232":
					//232 thunderstorm with heavy drizzle  11d
					Debug.Log ("stormy waves");
					OnValueChanged (5);
					break;
				case "300":
				case "301":
				case "302":
				case "310":
				case "311":
				case "312":
				case "313":
				case "314":
				case "321":
					//300 light intensity drizzle  09d
					//301 drizzle  09d
					//302 heavy intensity drizzle  09d
					//310 light intensity drizzle rain     09d
					//311 drizzle rain     09d
					//312 heavy intensity drizzle rain     09d
					//313 shower rain and drizzle  09d
					//314 heavy shower rain and drizzle    09d
					//321 shower drizzle   09d
					Debug.Log ("drizzle waves");
					OnValueChanged (3);
					break;
				case "500":
				case "501":			
					//500 light rain   10d
					//501 moderate rain    10d
					Debug.Log ("light rain waves");
					OnValueChanged (3);
					break;
				case "502":
				case "503":
				case "504":
				case "511":
					//502 heavy intensity rain     10d
					//503 very heavy rain  10d
					//504 extreme rain     10d
					//511 freezing rain    13d
					Debug.Log ("heavy rain waves");
					OnValueChanged (4);
					break;
				case "520":
				case "521":
					//520 light intensity shower rain  09d
					//521 shower rain  09d
					Debug.Log ("light showers rain waves");
					OnValueChanged (3);
					break;
				case "522":
				case "531":
					//522 heavy intensity shower rain  09d
					//531 ragged shower rain   09d
					Debug.Log ("heavy showers rain waves");
					OnValueChanged (4);
					break;
				case "600":
				case "601":
				case "602":
				case "611":
				case "612":
				case "615":
				case "616":
				case "620":
				case "621":
				case "622":
					//Group 6xx: Snow
					//600 light snow[[file:13d.png]]
					//601 snow[[file:13d.png]]
					//602 heavy snow[[file:13d.png]]
					//611 sleet[[file:13d.png]]
					//612 shower sleet[[file:13d.png]]
					//615 light rain and snow[[file:13d.png]]
					//616 rain and snow[[file:13d.png]]
					//620 light shower snow[[file:13d.png]]
					//621 shower snow[[file:13d.png]]
					//622 heavy shower snow[[file:13d.png]]
					Debug.Log ("snow waves");
					OnValueChanged (3);
					break;
				case "701":
				case "711":
				case "721":
				case "731":
				case "741":
				case "751":
				case "761":
				case "762":
				case "771":
				case "781":
					//Group 7xx: Atmosphere
					//701 mist[[file:50d.png]]
					//711 smoke[[file:50d.png]]
					//721 haze[[file:50d.png]]
					//731 sand, dust whirls[[file:50d.png]]
					//741 fog[[file:50d.png]]
					//751 sand[[file:50d.png]]
					//761 dust[[file:50d.png]]
					//762 volcanic ash[[file:50d.png]]
					//771 squalls[[file:50d.png]]
					//781 tornado[[file:50d.png]]
					Debug.Log ("atmosphere waves");
					OnValueChanged (3);
					break;
				case "800":
					//Group 800: Clear
					//800 clear sky[[file:01d.png]] [[file:01n.png]]
					Debug.Log ("clear sky waves");
					OnValueChanged (2);
					break;
				case "801":
					//Group 80x: Clouds
					//801	few clouds[[file:02d.png]] [[file:02n.png]]
					Debug.Log ("few clouds waves");
					OnValueChanged (2);
					break;
				case "802":
					//802	scattered clouds[[file:03d.png]] [[file:03d.png]]
					Debug.Log ("scattered clouds waves");
					OnValueChanged (3);
					break;
				case "803":
					//803	broken clouds[[file:04d.png]] [[file:03d.png]]
					Debug.Log ("broken clouds waves");
					OnValueChanged (3);
					break;
				case "804":
					//804	overcast clouds[[file:04d.png]] [[file:04d.png]]
					Debug.Log ("overcast clouds waves");
					OnValueChanged (3);
					break;
				case "900":
				case "901":
				case "902":
				case "903":
				case "904":
				case "905":
				case "906":
					//Group 90x: Extreme
					//900	tornado
					//901	tropical storm
					//902	hurricane
					//903	cold
					//904	hot
					//905	windy
					//906	hail
					Debug.Log ("extreme waves");
					OnValueChanged (6);
					break;
				case "951":
				case "952":
				case "953":
				case "954":
				case "955":
					//Group 9xx: Additional
					//951	calm
					//952	light breeze
					//953	gentle breeze
					//954	moderate breeze
					Debug.Log ("fresh breeze waves");
					OnValueChanged (1);
					break;
				case "956":
					Debug.Log ("strong breeze waves");
					OnValueChanged (4);
					break;
				case "957":
					Debug.Log ("high wind waves");
					OnValueChanged (4);
					break;
				case "958":
				case "959":
					Debug.Log ("severe gale waves");
					OnValueChanged (5);
					break;
				case "960":
				case "961":
					Debug.Log ("storm waves");
					OnValueChanged (5);
					break;
				case "962":
					Debug.Log ("hurricane waves");
					OnValueChanged (6);
					break;
				default:
					Debug.Log ("unexpected weather id waves");
					OnValueChanged (3);
					break;
				}

				yield return new WaitForSeconds (3600);
			}
		}

		void Update()
		{
			if(!float.IsNaN(changeTime))
			{
				float p = Mathf.Clamp01((Time.time - changeTime) / 30.0f);

				water.SetProfiles(
					new Water.WeightedProfile(sourceProfile, 1.0f - p),
					new Water.WeightedProfile(targetProfile, p)
				);

				progressSlider.value = p;

				if(p == 1.0f)
				{
					p = float.NaN;
					changeTime = float.NaN;
					progressSlider.transform.parent.gameObject.SetActive(false);

#if !UNITY_5_0 && !UNITY_5_1
					dropdown.interactable = true;
#endif
				}
			}
		}

		private void OnValueChanged(int index)
		{
			sourceProfile = targetProfile;
			targetProfile = profiles[index];
			changeTime = Time.time;

			progressSlider.transform.parent.gameObject.SetActive(true);

#if !UNITY_5_0 && !UNITY_5_1
			dropdown.interactable = false;
#endif
		}
	}
}
