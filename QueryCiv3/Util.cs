using System;
using System.IO;
using Blast;
using System.Text.RegularExpressions;
using System.Text;

namespace QueryCiv3 {
	public struct ByteBitmap {
		private byte Flags;
		public bool this[int i] { get => ((Flags >> i) & 1) == 1; }
	}

	public struct IntBitmap {
		private int Flags;
		public bool this[int i] { get => ((Flags >> i) & 1) == 1; }
	}

	public class Util {
		// Encoding code page ID; 1252 is Civ3 encoding for US language version
		public static Encoding Civ3Encoding;

		static Util() {
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			Civ3Encoding = Encoding.GetEncoding(1252);
		}

		public static byte[] ReadFile(string pathName) {
			byte[] MyFileData = File.ReadAllBytes(pathName);
			if (MyFileData[0] == 0x00 && (MyFileData[1] == 0x04 || MyFileData[1] == 0x05 || MyFileData[1] == 0x06)) {
				return Decompress(MyFileData);
			}
			return MyFileData;
		}

		public static byte[] Decompress(byte[] compressedBytes) {
			MemoryStream DecompressedStream = new MemoryStream();
			BlastDecoder Decompressor = new BlastDecoder(new MemoryStream(compressedBytes, writable: false), DecompressedStream);
			Decompressor.Decompress();
			return DecompressedStream.ToArray();
		}

		public static string GetString(byte[] bytes) {
			string Out = Civ3Encoding.GetString(bytes);
			Regex TrimAfterNull = new Regex(@"^[^\0]*");
			Match NoNullMatch = TrimAfterNull.Match(Out);
			return NoNullMatch.Value;
		}

		public static unsafe string GetString<T>(ref T structData, int start, int length) where T : unmanaged {
			byte[] Arr = new byte[length];
			fixed (void* dataPtr = &structData, arrPtr = Arr) {
				Buffer.MemoryCopy(((byte*)dataPtr) + start, (byte*)arrPtr, length, length);
			}
			return GetString(Arr);
		}

		public static bool GetFlag(byte flags, int index) {
			return ((flags >> index) & 1) == 1;
		}

		public static unsafe int GetInt<T>(ref T structData, int start) where T : unmanaged {
			byte[] Arr = new byte[4];
			fixed (void* dataPtr = &structData, arrPtr = Arr) {
				Buffer.MemoryCopy(((byte*)dataPtr) + start, (byte*)arrPtr, 4, 4);
			}
			return BitConverter.ToInt32(Arr, 0);
		}
	}
}
