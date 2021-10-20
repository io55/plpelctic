using AutoResxTranslator;
using CommonBotLibrary.Services;
using CommonBotLibrary.Services.Models;
using DictionaryLib;
using gnuciDictionary;
using QRCoder;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Media;
using System.Net;
using System.Speech.Synthesis;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using File = System.IO.File;

namespace PLEPPCLTYIC_Telegram
{
	public class MessageInfo
	{
		public string _From = "(NULL)";
		public string _Says = "(NULL)";
		public string _In = "(NULL)";

		public MessageInfo(Message source)
		{
			User whoFrom = source.From;
			Chat whatIn = source.Chat;
			switch (whoFrom.Username)
			{
				case null:
					_From = whoFrom.FirstName;
					if (!string.IsNullOrEmpty(whoFrom.LastName))
					{
						_From += " " + whoFrom.LastName;
					}
					break;
				default:
					_From = whoFrom.Username;
					break;
			}

			if (source.Text != null || source.Caption != null)
			{
				_Says = source.Text ?? source.Caption;
			}

			switch (whatIn.Title)
			{
				case null:
					if (whatIn.Username != null)
					{
						_In = whatIn.Username;
						Console.WriteLine($"{whatIn.Username}");
					}
					else
					{
						_In = whatIn.FirstName;
						if (!string.IsNullOrEmpty(whatIn.LastName))
						{
							_In += " " + whatIn.LastName;
						}
					}
					break;
				default:
					_In = whatIn.Title;
					break;
			}
		}
	}

	public class TranslateQuery
	{
		public string q;
		public string source;
		public string target;
	}

	public static class Utillity
	{
		public static string GenerateTruth(Random _Random, int seed = 0)
		{
			if (seed != 0)
			{
				_Random = new(seed);
			}

			if (!Directory.Exists("listopad"))
			{
				return "";
			}

			string[] files = Directory.GetFiles("listopad");
			if (files.Length == 0)
			{
				return "";
			}

			using StreamReader fStream = new(files[_Random.Next(files.Length)]);
			List<string> lines = new();
			string line = string.Empty;
			while ((line = fStream.ReadLine()) != null)
			{
				lines.Add(line);
			}

			string word = string.Empty;
			for (int i = 0; i < _Random.Next(1, 70); i++)
			{
				word += $"{lines[_Random.Next(lines.Count)].Trim()} ";
			}
			word = string.Join(' ', word.Split(" ").OrderBy(x => _Random.Next()));
			word = new Regex(@"(^[a-z])|\.\s+(.)|\!\s+(.)", RegexOptions.ExplicitCapture).Replace(word.ToLower(), s => s.Value.ToUpper());

			return word;
		}
	}

	public class Mutator
	{
		private static readonly Random random = new();

		private static string GenerateRandomBytes(int length)
		{
			string retval = string.Empty;
			for (int i = 0; i < length; i++)
			{
				retval += $"{random.Next(0xFF):X}";
			}
			return retval;
		}

		private static string GenerateRandomAscii(int length)
		{
			string retval = string.Empty;

			for (int i = 0; i < length; i++)
			{
				char next = (char)random.Next(0xFF);
				if (next == '\r' || next == '\n')
				{
					i--;
					continue;
				}

				retval += next;
			}

			return retval;
		}

		private static string InsertRandomBytes(string input, int randMaxAmt)
		{
			string retval = input;

			int randomPosition = random.Next(retval.Length);
			int byteCount = random.Next(randMaxAmt);

			return retval.Insert(randomPosition, GenerateRandomBytes(byteCount).ToLower());
		}

		private static string InsertRandomAscii(string input, int randMaxAmt)
		{
			string retval = input;

			int randomPosition = random.Next(retval.Length);
			int asciiCount = random.Next(randMaxAmt);

			return retval.Insert(randomPosition, GenerateRandomAscii(asciiCount));
		}

		public static string Output(string inp)
		{
			string firstpass = inp;

			int length = firstpass.Length;
			for (int i = 0; i < length; i++)
			{
				int rng = random.Next(10);
				switch (rng)
				{
					case <= 1:
						firstpass += " ";
						break;
					case > 1 and <= 4:
						firstpass = InsertRandomBytes(firstpass, 8);
						break;
					case > 4 and < 9:
						firstpass = InsertRandomAscii(firstpass, 8);
						break;
					case 9:
						{
							firstpass = string.Join(" ", firstpass.Split(" ").OrderBy(a => random.Next()));

							firstpass += " " + Utillity.GenerateTruth(random) + " ";
							break;
						}

					default:
						break;
				}
			}

			return firstpass;
		}
	}

	public class BotManager
	{
		private readonly ITelegramBotClient _Client = null;
		private static readonly DictionaryLib.DictionaryLib _Dictionary = new(DictionaryType.Small);
		private static Random _Random = new();

		public BotManager(string token)
		{
			_Client = new TelegramBotClient(token);

			_Client.OnMessage += Client_OnMessage;
		}

		public void Run()
		{
			_Client.StartReceiving();

			Console.WriteLine("Press any key to exit");
			Console.ReadKey();

			_Client.StopReceiving();
		}

		private async void Client_OnMessage(object sender, MessageEventArgs e)
		{
			if (e == null || e.Message == null || (e.Message.Text == null && e.Message.Caption == null))
			{
				return;
			}

			MessageInfo info = new(e.Message);
			Console.WriteLine($"\"{info._From}\" in \"{info._In}\" | \"{info._Says}\"");

			string checkCmd = info._Says.Trim().ToLower();
			switch (checkCmd)
			{
				case string when checkCmd.StartsWith("pl translate"):
					{
						string s = info._Says.Trim();
						if (s.Length == "pl translate".Length)
						{
							break;
						}

						string translated = TranslateMessage(s.Replace("pl translate", "").Trim());
						await ReplyToMessage(e.Message, translated ?? "Unable to translate, try again later");
						return;
					}
				case string when checkCmd.StartsWith("pl truth"):
					{
						string s = info._Says.Trim();
						string[] words = s.Split();
						if (words.Length != 3 || !int.TryParse(words[2], out int result))
						{
							await ReplyToMessage(e.Message, Utillity.GenerateTruth(_Random));
							return;
						}

						result = Math.Clamp(result, 1, 10);
						for (int i = 0; i < result; i++)
						{
							await ReplyToMessage(e.Message, Utillity.GenerateTruth(_Random));
						}
						return;
					}
				case string when checkCmd.StartsWith("pl image"):
					{
						string s = info._Says.Replace("pl image", "").Trim();

						Bitmap image = GenerateImage(s);
						using MemoryStream imageStream = new();
						image.Save(imageStream, ImageFormat.Png);
						imageStream.Position = 0;

						await ReplyToMessage(e.Message, imageStream, MessageStreamType.Image);

						return;
					}
				case string when checkCmd.StartsWith("pl arck"):
					{
						string s = info._Says.Trim();
						string[] words = s.Split();
						if (words.Length != 3 || !int.TryParse(words[2], out int result))
						{
							await ReplyToMessage(e.Message, GenerateWord(2));
							return;
						}

						result = Math.Clamp(result, 1, 10);
						for (int i = 0; i < result; i++)
						{
							await ReplyToMessage(e.Message, GenerateWord(2));
						}
						return;
					}
				case string when checkCmd.StartsWith("pl info"):
					{
						await ReplyToMessage(e.Message, "Friend bot to anyone esoterically generic enough to ascertain ascension");
						return;
					}
				case string when checkCmd.StartsWith("pl answer"):
					{
						await ReplyToMessage(e.Message, new Random(checkCmd.GetHashCode()).Next(2) == 0 ? "Yes" : "No");
						return;
					}
				case string when checkCmd.StartsWith("pl help"):
					{
						await ReplyToMessage(e.Message, @"Commands:
- pl info
- pl dict

- pl truth [1-10]
- pl arck [1-10]

- pl qr [text]
- pl image [text]
- pl speak [text]
- pl mutate [text]
- pl translate [text]

- pl upload [image]
- pl reddit [subreddit]
- pl answer [yes/no question]
- pl eval [mathematical expression, functions such as Sin, Cos & Tan need to have an uppercase first letter]
- pl d (enter dynamic mode, you can use the ""print"" command, ""set [address] [value]"" on different lines to each other)");
						return;
					}
				case string when checkCmd.StartsWith("pl eval"):
					{
						string toEval = e.Message.Text.Replace("pl eval", "").Trim();
						try
						{
							NCalcService service = new();

							string res = await service.EvaluateAsync(toEval);
							await ReplyToMessage(e.Message, res);
						}
						catch (Exception ex)
						{
							await ReplyToMessage(e.Message, $"Unable to parse, {ex.Message}");
						}
						return;
					}
				case string when checkCmd.StartsWith("pl reddit"):
					{
						string toEval = e.Message.Text.Replace("pl reddit", "").Trim();
						HandleReddit(toEval, e.Message);
						return;
					}
				case string when checkCmd.StartsWith("pl dict"):
					{
						await ReplyToMessage(e.Message, "The Github version of plpelctic does not have this feature, to see the official dictionary, go to axiot.us/dictionary.txt");
						return;
					}
				case string when checkCmd.StartsWith("pl mutate"):
					{
						string toEval = e.Message.Text.Replace("pl mutate", "").Trim();
						await ReplyToMessage(e.Message, Mutator.Output(toEval));
						return;
					}
				case string when checkCmd.StartsWith("pl speak"):
					{
						if (OperatingSystem.IsWindows())
						{
							using SpeechSynthesizer synth = new();
							using MemoryStream streamAudio = new();
							SoundPlayer soundPlayer = new();

							synth.SelectVoice(_Random.Next(2) == 1 ? "Microsoft Hazel Desktop" : "Microsoft Zira Desktop");
							synth.SetOutputToWaveStream(streamAudio);
							string toSpeak = e.Message.Text.Replace("pl speak", "").Trim();
							if (toSpeak == string.Empty)
							{
								return;
							}

							synth.Speak(toSpeak);

							streamAudio.Position = 0;
							soundPlayer.Stream = streamAudio;
							soundPlayer.Play();

							synth.SetOutputToNull();
							soundPlayer.Stream.Position = 0;
							await ReplyToMessage(e.Message, (MemoryStream)soundPlayer.Stream, MessageStreamType.Audio);

							soundPlayer.Dispose();
							streamAudio.Dispose();
							synth.Dispose();
							GC.Collect();
						}
						return;
					}
				case string when checkCmd.StartsWith("pl qr"):
					{
						string toQR = e.Message.Text.Replace("pl qr", "").Trim();

						try
						{
							QRCodeGenerator qrGenerator = new();
							QRCodeData qrCodeData = qrGenerator.CreateQrCode(toQR, QRCodeGenerator.ECCLevel.Q);
							QRCode qrCode = new(qrCodeData);
							Bitmap qrCodeImage = qrCode.GetGraphic(20, Color.DarkRed, Color.Black, true);

							using MemoryStream imageStream = new();
							qrCodeImage.Save(imageStream, ImageFormat.Png);
							imageStream.Position = 0;

							await ReplyToMessage(e.Message, imageStream, MessageStreamType.Image);

						}
						catch (Exception ex)
						{
							await ReplyToMessage(e.Message, ex.Message);
						}

						return;
					}
				case string when checkCmd.StartsWith("pl upload"):
					{
						PhotoSize[] photos = e.Message.Photo;
						if (photos == null)
						{
							return;
						}

						try
						{
							PhotoSize photo = photos[^1];
							Telegram.Bot.Types.File file = await _Client.GetFileAsync(photo.FileId);

							string[] files = Directory.GetFiles("uploaded/");
							int highestName = 0;
							foreach (string fileName in files)
							{
								int name = int.Parse(Path.GetFileNameWithoutExtension(fileName));

								if (name > highestName)
								{
									highestName = name;
								}
							}

							using FileStream fs = new($"uploaded/{highestName + 1}.png", FileMode.Create);
							await _Client.DownloadFileAsync(file.FilePath, fs);
						}
						catch (Exception ex)
						{
							await ReplyToMessage(e.Message, ex.Message);
						}

						return;
					}

				case "pl" or "hi":
					{
						bool hi = checkCmd == "hi" && _Random.Next(10) == 1;

						User from = e.Message.From;
						string name = string.Empty;

						if (from.Username == null)
						{
							if (from.FirstName != null)
							{
								name = from.FirstName;
							}
							if (from.LastName != null)
							{
								name += $" {from.LastName}";
							}
						}
						else
						{
							name = from.Username;
						}

						await ReplyToMessage(e.Message, hi ? "hi " + name : name);
						return;
					}

				// Trolling messages
				case string when checkCmd.Contains("hehh"):
					{
						if (_Random.Next(10) <= 3)
						{
							await ReplyToMessage(e.Message, "^^ This 👁");
						}
						return;
					}
				case string when checkCmd.Contains("honestly"):
					{
						if (_Random.Next(10) <= 3)
						{
							string question = "?";
							for (int i = 0; i < _Random.Next(20); i++)
							{
								question += "?";
							}

							await ReplyToMessage(e.Message, $"🙄 Rdly{question}");
						}
						return;
					}
				case string when checkCmd.EndsWith("?"):
					{
						if (_Random.Next(10) <= 3)
						{
							string output = "well ";
							for (int i = 0; i < _Random.Next(21); i++)
							{
								output += "well ";
							}
							await ReplyToMessage(e.Message, output.Trim());
						}
						return;
					}
				default:
					break;
			}
		}

		private Task ReplyToMessage(Message repTo, string msg)
		{
			try
			{
				if (repTo.MessageId != 0)
				{
					_Client.SendTextMessageAsync(repTo.Chat, msg, replyToMessageId: repTo.MessageId);
				}
				else
				{
					_Client.SendTextMessageAsync(repTo.Chat, msg);
				}
			}
			catch (Exception ex)
			{
				if (repTo.MessageId != 0)
				{
					_Client.SendTextMessageAsync(repTo.Chat, ex.Message, replyToMessageId: repTo.MessageId);
				}
				else
				{
					_Client.SendTextMessageAsync(repTo.Chat, ex.Message);
				}
			}
			return Task.CompletedTask;
		}

		public enum MessageStreamType
		{
			Image,
			Audio
		}


		private Task ReplyToMessage(Message repTo, MemoryStream stream, MessageStreamType type)
		{
			try
			{
				switch (type)
				{
					case MessageStreamType.Image:
						if (repTo.MessageId != 0)
						{
							_Client.SendPhotoAsync(repTo.Chat, stream, replyToMessageId: repTo.MessageId);
						}
						else
						{
							_Client.SendPhotoAsync(repTo.Chat, stream);
						}
						break;
					case MessageStreamType.Audio:
						if (repTo.MessageId != 0)
						{
							_Client.SendAudioAsync(repTo.Chat, stream, replyToMessageId: repTo.MessageId);
						}
						else
						{
							_Client.SendAudioAsync(repTo.Chat, stream);
						}
						break;
					default:
						break;
				}
			}
			catch (Exception ex)
			{
				if (repTo.MessageId != 0)
				{
					_Client.SendTextMessageAsync(repTo.Chat, ex.Message, replyToMessageId: repTo.MessageId);
				}
				else
				{
					_Client.SendTextMessageAsync(repTo.Chat, ex.Message);
				}
			}
			return Task.CompletedTask;
		}

		private static string TranslateMessage(string input)
		{
			for (int i = 0; i < 15; i++)
			{
				if (_Random.Next(15) == 5)
				{
					input += _Dictionary.RandomWord() + " ";
				}
			}

			input = input.Trim();

			List<string> wordsDirty = input.Split(" ").ToList();
			foreach (string word in input.Split(" ").ToList())
			{
				if (_Random.Next(0, 10) == 3)
				{
					continue;
				}

				IEnumerable<Word> definition = EnglishDictionary.Define(word);
				if (definition != null && definition.Any())
				{
					wordsDirty.Add(definition.First().Definition.Replace("pl.", ""));
				}
			}

			wordsDirty = wordsDirty.OrderBy(i => Guid.NewGuid()).ToList();
			input = string.Join(" ", wordsDirty).Trim();

			GTranslateService.Translate(input, "en", "be", out string result);
			result = result.Where(c => !char.IsPunctuation(c)).Aggregate("", (current, c) => current + c);
			GTranslateService.Translate(result, "be", "hr", out result);
			result = string.Join(" ", result.Split(" ").OrderBy(i => _Random.Next()));
			GTranslateService.Translate(result, "hr", "pt", out result);
			GTranslateService.Translate(result, "pt", "ko", out result);

			return GTranslateService.Translate(result, "ko", "en", out result) ? result : null;
		}


		private static Bitmap GenerateImage(string s)
		{
			int hashCode = s.Length == 0 ? _Random.Next() : s.GetHashCode();
			Random rng = new(hashCode);

			Bitmap img = new(500, 500);
			Graphics gfx = Graphics.FromImage(img);

			gfx.Clear(Color.Black);

			int bgs = rng.Next(3);
			int bgs2C = rng.Next(1, 6);
			for (int x = 0; x < img.Width; x++)
			{
				for (int y = 0; y < img.Height; y++)
				{
					switch (bgs)
					{
						case 0:
							if (x == 0 || x == 1 || x == img.Width - 1 || x == img.Width - 2
				 || y == 0 || y == 1 || y == img.Height - 1 || y == img.Height - 2)
							{
								continue;
							}

							if ((x % 4 == 0 && y % 4 == 3)
							 || (x % 4 == 1 && y % 4 == 2)
							 || (x % 4 == 2 && y % 4 == 1)
							 || (x % 4 == 3 && y % 4 == 0))
							{
								img.SetPixel(x, y, Color.White);
							}
							break;

						case 1:
							if (x == 0 || x == 1 || x == img.Width - 1 || x == img.Width - 2
	|| y == 0 || y == 1 || y == img.Height - 1 || y == img.Height - 2)
							{
								continue;
							}

							if ((x % 6 == bgs2C + 1 && y % 6 == bgs2C)
							 || (x % 6 == bgs2C - 1 && y % 6 == bgs2C + 2)
							 || (x % 6 == bgs2C + 2 && y % 6 == bgs2C - 1)
							 || (x % 6 == bgs2C && y % 6 == bgs2C + 1))
							{
								img.SetPixel(x, y, Color.White);
							}
							break;

						default:
							continue;
					}
				}
			}

			Font fontBig = new("Old English Text MT", 64);
			Font fontSmall = new("Old English Text MT", 16);

			for (int i = 0; i < rng.Next(5, 250); i++)
			{
				string[] files = Directory.GetFiles("uploaded/");

				gfx.DrawImage(Image.FromFile(files[rng.Next(files.Length)]), rng.Next(500), rng.Next(500),
					(float)rng.NextDouble() * 500, (float)rng.NextDouble() * 500);

				gfx.DrawImage(Image.FromFile(files[rng.Next(files.Length)]), rng.Next(500), rng.Next(500),
	(float)rng.NextDouble() * 100, (float)rng.NextDouble() * 100);

				try
				{
					gfx.RotateTransform((float)rng.NextDouble() * 360);
					gfx.ScaleTransform(rng.Next(-1, 2) + (float)rng.NextDouble(), rng.Next(-1, 2) + (float)rng.NextDouble());

					for (int j = 0; j < rng.Next(2, 20); j++)
					{
						gfx.DrawString(Utillity.GenerateTruth(_Random, hashCode), fontBig, new SolidBrush(Color.Tomato), rng.Next(img.Width), rng.Next(img.Height));
						hashCode = rng.Next();
					}

					hashCode = rng.Next();
					gfx.DrawString(Utillity.GenerateTruth(_Random, hashCode), fontSmall, new SolidBrush(Color.Black), rng.Next(img.Width), rng.Next(img.Height));
					hashCode = rng.Next();
					gfx.DrawString(Utillity.GenerateTruth(_Random, hashCode), fontSmall, new SolidBrush(Color.Aquamarine), rng.Next(img.Width), rng.Next(img.Height));
					hashCode = rng.Next();
					gfx.DrawString(Utillity.GenerateTruth(_Random, hashCode), fontSmall, new SolidBrush(Color.AliceBlue), rng.Next(img.Width), rng.Next(img.Height));
				}
				catch (Exception)
				{
					gfx.ResetTransform();
					gfx.ResetClip();

					hashCode = rng.Next();
					gfx.DrawString(Utillity.GenerateTruth(_Random, hashCode), fontSmall, new SolidBrush(Color.Black), rng.Next(img.Width), rng.Next(img.Height));
					hashCode = rng.Next();
					gfx.DrawString(Utillity.GenerateTruth(_Random, hashCode), fontSmall, new SolidBrush(Color.Aquamarine), rng.Next(img.Width), rng.Next(img.Height));
					hashCode = rng.Next();
					gfx.DrawString(Utillity.GenerateTruth(_Random, hashCode), fontSmall, new SolidBrush(Color.AliceBlue), rng.Next(img.Width), rng.Next(img.Height));
				}
			}

			return img;
		}

		private static string GenerateWord(int suffixCount)
		{
			List<string> usedPrefixes = new();
			string word = string.Empty;

			for (int i = 0; i < suffixCount; i++)
			{
				string prefix = string.Empty;
				while (usedPrefixes.Contains(prefix) || prefix == string.Empty)
				{
					prefix = ArckDefinitions.Prefix[_Random.Next(ArckDefinitions.Prefix.Length)];
				}
				usedPrefixes.Add(prefix);
			}

			for (int i = 0; i < usedPrefixes.Count; i++)
			{
				// if it isn't the final prefix
				if (i != usedPrefixes.Count - 1)
				{
					// check if the end of the first word and the start of the next word are colliding
					if (usedPrefixes[i][^1] == usedPrefixes[i + 1][0])
					{
						usedPrefixes[i] = usedPrefixes[i].Remove(usedPrefixes[i].Length - 1);
					}
				}

				word += usedPrefixes[i].Trim();
			}

			string suffix = ArckDefinitions.Suffix[_Random.Next(0, ArckDefinitions.Suffix.Length)];
			if (suffix.StartsWith(word[^1]))
			{
				suffix = suffix.Remove(0, 1);
			}

			return word + suffix.Trim();
		}

		private async void HandleReddit(string request, Message originalMessage)
		{
			string[] broken = request.Split(" ");
			int amount = 1;
			if (broken.Length == 2)
			{
				if (int.TryParse(broken[1], out amount))
				{
					amount = Math.Clamp(amount, 1, 5);
				}
			}
			try
			{
				RedditService service = new();
				IEnumerable<RedditSharp.Things.Post> res = await service.GetPostsAsync(broken[0], RedditResult.PostCategory.New, amount);
				if (res == null)
				{
					return;
				}

				if (res.ElementAt(0) == null)
				{
					return;
				}

				RedditSharp.Things.Post first = res.ElementAt(0);

				string toPrint = $"[AUTHOR] {first.AuthorName}\n[TITLE] {first.Title}\n{first.SelfText}" ?? $"[CONTENT] {first.SelfText}";
				ReplyToMessage(originalMessage, toPrint).ConfigureAwait(false).GetAwaiter().GetResult();
				if (first.Thumbnail == null)
				{
					return;
				}

				try
				{
					HttpWebRequest webRequest = WebRequest.Create(first.Thumbnail) as HttpWebRequest;
					webRequest.AllowWriteStreamBuffering = true;

					WebResponse webResponse = webRequest.GetResponse();

					MemoryStream stream = new();
					webResponse.GetResponseStream().CopyTo(stream);
					stream.Position = 0;

					ReplyToMessage(originalMessage, stream, MessageStreamType.Image).ConfigureAwait(false).GetAwaiter().GetResult();

					webResponse.Close();
				}
				catch (Exception)
				{

				}
			}
			catch (Exception)
			{
				await ReplyToMessage(originalMessage, $"Invalid subreddit");
			}
		}
	}

	internal class Program
	{
		private static void Main() => new BotManager(File.ReadAllText("secret.txt")).Run();
	}
}
