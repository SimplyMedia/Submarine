using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Submarine.Core.Release.Exceptions;
using Submarine.Core.Release.Util;

namespace Submarine.Core.Validator
{
	public class UsenetReleaseValidatorService : IValidator<string>
	{
		private static readonly Regex[] RejectHashedReleasesRegexes =
		{
			// Generic match for md5 and mixed-case hashes.
			new(@"^[0-9a-zA-Z]{32}", RegexOptions.Compiled),

			// Generic match for shorter lower-case hashes.
			new(@"^[a-z0-9]{24}$", RegexOptions.Compiled),

			// Format seen on some NZBGeek releases
			// Be very strict with these coz they are very close to the valid 101 ep numbering.
			new(@"^[A-Z]{11}\d{3}$", RegexOptions.Compiled),
			new(@"^[a-z]{12}\d{3}$", RegexOptions.Compiled),

			//Backup filename (Unknown origins)
			new(@"^Backup_\d{5,}S\d{2}-\d{2}$", RegexOptions.Compiled),

			//123 - Started appearing December 2014
			new(@"^123$", RegexOptions.Compiled),

			//abc - Started appearing January 2015
			new(@"^abc$", RegexOptions.Compiled | RegexOptions.IgnoreCase),

			//abc - Started appearing 2020
			new(@"^abc[-_. ]xyz", RegexOptions.Compiled | RegexOptions.IgnoreCase),

			//b00bs - Started appearing January 2015
			new(@"^b00bs$", RegexOptions.Compiled | RegexOptions.IgnoreCase),

			// 170424_26 - Started appearing August 2018
			new(@"^\d{6}_\d{2}$"),

			// additional Generic match for mixed-case hashes. - Started appearing Dec 2020
			new(@"^[0-9a-zA-Z]{30}", RegexOptions.Compiled),

			// additional Generic match for mixed-case hashes. - Started appearing Jan 2021
			new(@"^[0-9a-zA-Z]{26}", RegexOptions.Compiled),

			// additional Generic match for mixed-case hashes. - Started appearing Jan 2021
			new(@"^[0-9a-zA-Z]{39}", RegexOptions.Compiled),

			// additional Generic match for mixed-case hashes. - Started appearing Jan 2021
			new(@"^[0-9a-zA-Z]{24}", RegexOptions.Compiled),
		};

		private readonly ILogger<UsenetReleaseValidatorService> _logger;

		public UsenetReleaseValidatorService(ILogger<UsenetReleaseValidatorService> logger)
			=> _logger = logger;

		public void Validate(string input)
		{
			var lowerInput = input.ToLower();
			
			if (lowerInput.Contains("password") && lowerInput.Contains("yenc"))
			{
				_logger.LogDebug("Rejected Encrypted Usenet Release Title: {Input}", input);
				throw new InvalidReleaseException($"Encrypted Usenet Release");
			}
			
			if (!input.Any(char.IsLetterOrDigit))
				throw new InvalidReleaseException($"No Letter or Digit in Release");

			var inputWithoutExtension = ReleaseUtil.RemoveFileExtension(input);

			if (!RejectHashedReleasesRegexes.Any(v => v.IsMatch(inputWithoutExtension))) return;
			
			_logger.LogDebug("Rejected Hashed Release Title: {Input}", input);
			throw new InvalidReleaseException($"Hashed Release");
		}
	}
}
