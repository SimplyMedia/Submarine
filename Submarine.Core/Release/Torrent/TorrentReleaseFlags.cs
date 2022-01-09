using System;

namespace Submarine.Core.Release.Torrent; 

[Flags]
public enum TorrentReleaseFlags
{
	NONE = 0,
	FREELEECH = 1 << 1,
	HALFLEECH = 1 << 2,
	DOUBLE_UPLOAD = 1 << 3,
	OTHER_PROMOTION = 1 << 4,
	SCENE = 1 << 5
}
