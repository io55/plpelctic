using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web;

/* 
 * AutoResxTranslator
 * by Salar Khalilzadeh
 * 
 * https://github.com/salarcode/AutoResxTranslator/
 * Mozilla Public License v2
 */
namespace AutoResxTranslator
{
	public class GTranslateService
	{
		private const string RequestUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:70.0) Gecko/20100101 Firefox/70.0";
		private const string RequestGoogleTranslatorUrl = "https://translate.googleapis.com/translate_a/single?client=gtx&sl={0}&tl={1}&hl=en&dt=t&dt=bd&dj=1&source=icon&tk=467103.467103&q={2}";

		public delegate void TranslateCallBack(bool succeed, string result);
		public static void TranslateAsync(
			string text,
			string sourceLng,
			string destLng,
			string textTranslatorUrlKey,
			TranslateCallBack callBack)
		{
			WebRequest request = CreateWebRequest(text, sourceLng, destLng);
			request.BeginGetResponse(
				TranslateRequestCallBack,
				new KeyValuePair<WebRequest, TranslateCallBack>(request, callBack));
		}

		public static bool Translate(
			string text,
			string sourceLng,
			string destLng,
			out string result)
		{
			WebRequest request = CreateWebRequest(text, sourceLng, destLng);
			try
			{
				HttpWebResponse response = (HttpWebResponse)request.GetResponse();

				if (response.StatusCode != HttpStatusCode.OK)
				{
					result = "Response is failed with code: " + response.StatusCode;
					return false;
				}

				using Stream stream = response.GetResponseStream();
				bool succeed = ReadGoogleTranslatedResult(stream, out string output);
				result = output;
				return succeed;
			}
			catch (Exception ex)
			{
				result = ex.Message;
				return false;
			}
		}

		private static WebRequest CreateWebRequest(
			string text,
			string lngSourceCode,
			string lngDestinationCode)
		{
			text = HttpUtility.UrlEncode(text);

			string url = string.Format(RequestGoogleTranslatorUrl, lngSourceCode, lngDestinationCode, text);

			HttpWebRequest create = (HttpWebRequest)WebRequest.Create(url);
			create.UserAgent = RequestUserAgent;
			create.Timeout = 50 * 1000;
			return create;
		}

		private static void TranslateRequestCallBack(IAsyncResult ar)
		{
			KeyValuePair<WebRequest, TranslateCallBack> pair = (KeyValuePair<WebRequest, TranslateCallBack>)ar.AsyncState;
			WebRequest request = pair.Key;
			TranslateCallBack callback = pair.Value;
			HttpWebResponse response = null;
			try
			{
				response = (HttpWebResponse)request.EndGetResponse(ar);
				if (response.StatusCode != HttpStatusCode.OK)
				{
					callback(false, "Response is failed with code: " + response.StatusCode);
					return;
				}

				using Stream stream = response.GetResponseStream();
				bool succeed = ReadGoogleTranslatedResult(stream, out string output);

				callback(succeed, output);
			}
			catch (Exception ex)
			{
				callback(false, "Request failed.\r\n" + ex.Message);
			}
			finally
			{
				response?.Close();
			}
		}

		/// <summary>
		///  the main trick :)
		/// </summary>
		private static bool ReadGoogleTranslatedResult(Stream rawdata, out string result)
		{
			string text;
			using (StreamReader reader = new(rawdata, Encoding.UTF8))
			{
				text = reader.ReadToEnd();
			}

			try
			{
				dynamic obj = SimpleJson.DeserializeObject(text);

				string final = "";

				// the number of lines
				int lines = obj[0].Count;
				for (int i = 0; i < lines; i++)
				{
					// the translated text.
					final += (obj[0][i][0]).ToString();
				}
				result = final;
				return true;
			}
			catch (Exception ex)
			{
				result = ex.Message;
				return false;
			}
		}

	}
}
