using System.Security.Cryptography;
using System.Text;
using BencodeNET.Exceptions;
using BencodeNET.Objects;
using BencodeNET.Parsing;
using BencodeNET.Torrents;

namespace Submarine.Core.Download;

/// <summary>
///     Computes and parses BitTorrent info hashes from .torrent files and magnet uris
/// </summary>
public static class TorrentInfoHash
{
	private static readonly BencodeParser Parser = new();

	private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

	/// <summary>
	///     Computes the SHA1 info hash of a .torrent file
	/// </summary>
	/// <param name="torrentData">Raw bytes of the .torrent file</param>
	/// <returns>The upper-case hex encoded info hash</returns>
	/// <exception cref="DownloadClientException">The data is not valid bencode or has no info dictionary</exception>
	public static string Compute(byte[] torrentData)
	{
		BDictionary root;
		try
		{
			root = Parser.Parse<BDictionary>(torrentData);
		}
		catch (BencodeException ex)
		{
			throw new DownloadClientException("Torrent file is not valid bencode", ex);
		}

		if (root.FirstOrDefault(pair => pair.Key.ToString() == "info").Value is not BDictionary info)
			throw new DownloadClientException("Torrent file has no info dictionary");

		return TorrentUtil.CalculateInfoHash(info).ToUpperInvariant();
	}

	/// <summary>
	///     Extracts the info hash from a magnet uri
	/// </summary>
	/// <param name="magnetUrl">The magnet uri</param>
	/// <returns>The upper-case hex encoded info hash, or null when the magnet has no usable btih hash</returns>
	public static string? FromMagnet(string magnetUrl)
	{
		if (!magnetUrl.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase))
			return null;

		var queryStart = magnetUrl.IndexOf('?');
		if (queryStart < 0) return null;

		foreach (var parameter in magnetUrl[(queryStart + 1)..].Split('&'))
		{
			if (!parameter.StartsWith("xt=urn:btih:", StringComparison.OrdinalIgnoreCase)) continue;

			var hash = parameter["xt=urn:btih:".Length..];
			return hash.Length == 40 && hash.All(IsHexDigit) ? hash.ToUpperInvariant()
				: hash.Length == 32 ? Convert.ToHexString(Base32Decode(hash))
				: null;
		}

		return null;
	}

	private static bool IsHexDigit(char c)
		=> c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F';

	private static byte[] Base32Decode(string input)
	{
		var output = new List<byte>(input.Length * 5 / 8);
		var buffer = 0;
		var bits = 0;

		foreach (var c in input)
		{
			var index = Base32Alphabet.IndexOf(char.ToUpperInvariant(c));
			if (index < 0)
				throw new DownloadClientException($"Magnet link has an invalid base32 info hash: {c}");

			buffer = (buffer << 5) | index;
			bits += 5;

			if (bits < 8) continue;

			output.Add((byte)(buffer >> (bits - 8)));
			bits -= 8;
		}

		return [.. output];
	}
}
