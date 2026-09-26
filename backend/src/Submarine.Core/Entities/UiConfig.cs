using Submarine.Core.Enums;

namespace Submarine.Core.Entities;

/// <summary>
///     Web UI preferences, singleton row with Id 1.
/// </summary>
public sealed class UiConfig : SingletonEntity
{
	/// <summary>Color theme.</summary>
	public Theme Theme { get; set; } = Theme.AUTO;

	/// <summary>0 is Sunday, 1 is Monday, following the DateTime day of week values.</summary>
	public int FirstDayOfWeek { get; set; }

	/// <summary>Column header format of the calendar week view.</summary>
	public string CalendarWeekColumnHeader { get; set; } = "ddd M/d";

	/// <summary>Short date format.</summary>
	public string ShortDateFormat { get; set; } = "MMM D, YYYY";

	/// <summary>Long date format.</summary>
	public string LongDateFormat { get; set; } = "dddd, MMMM D, YYYY";

	/// <summary>Time format.</summary>
	public string TimeFormat { get; set; } = "h(:mm)a";

	/// <summary>Show relative dates like "in 2 hours".</summary>
	public bool ShowRelativeDates { get; set; } = true;

	/// <summary>UI language.</summary>
	public string Language { get; set; } = "en";
}
