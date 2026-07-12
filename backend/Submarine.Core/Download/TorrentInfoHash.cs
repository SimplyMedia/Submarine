using System.Security.Cryptography;
using System.Text;

namespace Submarine.Core.Download;

/// <summary>
///     Computes the info hash of a .torrent file from its bencoded contents
/// </summary>
public static class TorrentInfoHash
{
	/// <summary>
	///     Computes the SHA1 info hash over the bencoded info dictionary of a .torrent file
	/// </summary>
	/// <param name="data">The raw bytes of the .torrent file</param>
	/// <returns>The uppercase hex encoded info hash</returns>
	/// <exception cref="DownloadClientException">The data is not a valid bencoded torrent with an info dictionary</exception>
	public static string Compute(byte[] data)
	{
		var position = 0;
		if (position >= data.Length || data[position++] != 'd')
			throw new DownloadClientException("Torrent file is not a bencoded dictionary");

		while (position < data.Length && data[position] != 'e')
		{
			var key = ReadBencodeString(data, ref position);
			var valueStart = position;
			SkipBencodeElement(data, ref position);

			if (key == "info")
				return Convert.ToHexString(SHA1.HashData(data.AsSpan(valueStart, position - valueStart)));
		}

		throw new DownloadClientException("Torrent file has no info dictionary");
	}

	private static string ReadBencodeString(byte[] data, ref int position)
	{
		var colon = Array.IndexOf(data, (byte)':', position);
		var length = int.Parse(Encoding.ASCII.GetString(data, position, colon - position));
		var value = Encoding.ASCII.GetString(data, colon + 1, length);
		position = colon + 1 + length;

		return value;
	}

	private static void SkipBencodeElement(byte[] data, ref int position)
	{
		switch ((char)data[position])
		{
			case 'i':
				position = Array.IndexOf(data, (byte)'e', position) + 1;
				break;
			case 'l':
			case 'd':
				position++;
				while (data[position] != 'e')
				{
					if (data[position] == 'd' || data[position] == 'l' || data[position] == 'i')
						SkipBencodeElement(data, ref position);
					else
						ReadBencodeString(data, ref position);
				}

				position++;
				break;
			default:
				ReadBencodeString(data, ref position);
				break;
		}
	}
}
