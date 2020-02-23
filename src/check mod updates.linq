<Query Kind="Program">
  <Reference>C:\source\_Stardew\SMAPI\bin\Debug\SMAPI.Toolkit\netstandard2.0\SMAPI.Toolkit.CoreInterfaces.dll</Reference>
  <Reference>C:\source\_Stardew\SMAPI\bin\Debug\SMAPI.Toolkit\netstandard2.0\SMAPI.Toolkit.dll</Reference>
  <NuGetReference>HtmlAgilityPack</NuGetReference>
  <NuGetReference Prerelease="true">MonkeyCache.FileStore</NuGetReference>
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <NuGetReference>Pathoschild.Http.FluentClient</NuGetReference>
  <Namespace>HtmlAgilityPack</Namespace>
  <Namespace>MonkeyCache</Namespace>
  <Namespace>MonkeyCache.FileStore</Namespace>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>Newtonsoft.Json.Linq</Namespace>
  <Namespace>Pathoschild.Http.Client</Namespace>
  <Namespace>StardewModdingAPI</Namespace>
  <Namespace>StardewModdingAPI.Toolkit</Namespace>
  <Namespace>StardewModdingAPI.Toolkit.Framework.Clients.WebApi</Namespace>
  <Namespace>StardewModdingAPI.Toolkit.Framework.Clients.Wiki</Namespace>
  <Namespace>StardewModdingAPI.Toolkit.Framework.ModData</Namespace>
  <Namespace>StardewModdingAPI.Toolkit.Framework.ModScanning</Namespace>
  <Namespace>StardewModdingAPI.Toolkit.Framework.UpdateData</Namespace>
  <Namespace>StardewModdingAPI.Toolkit.Serialization</Namespace>
  <Namespace>StardewModdingAPI.Toolkit.Serialization.Models</Namespace>
  <Namespace>StardewModdingAPI.Toolkit.Utilities</Namespace>
  <Namespace>System.Globalization</Namespace>
  <Namespace>System.Net</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

/*

  This script...
     1. scans the Mods folder for installed mods;
	 2. crossreferences each mod to the update API and wiki;
	 3. displays the metadata for review, with available updates shown first.

*/

/*********
** Configuration
*********/
/****
** Environment
****/
/// <summary>The absolute path for the folder containing mods.</summary>
private readonly string GameFolderPath = @"C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley";

/// <summary>The absolute path for the folder containing mods.</summary>
private string ModFolderPath => Path.Combine(this.GameFolderPath, "Mods (test)");

/// <summary>The absolute path for the file which, if present, indicates mod folders should not be normalized.</summary>
private string ModFolderPathDoNotNormalizeToken => Path.Combine(this.ModFolderPath, "DO_NOT_NORMALIZE.txt");

/// <summary>The absolute path for SMAPI's metadata file.</summary>
private string MetadataFilePath => Path.Combine(this.GameFolderPath, "smapi-internal", "StardewModdingAPI.metadata.json");

/// <summary>The application ID for the mod data cache.</summary>
private readonly string CacheApplicationKey = @"smapi";

/// <summary>How long mod data should be cached.</summary>
private readonly TimeSpan CacheTime = TimeSpan.FromMinutes(30);

/****
** Common settings
****/
/// <summary>The wiki compatibility statuses to highlight as errors. Mainly useful when you have a set of mods you know work or don't work, and want to find errors in the compatibility list.</summary>
private readonly HashSet<WikiCompatibilityStatus> HighlightStatuses = new HashSet<WikiCompatibilityStatus>(
	// all statuses
	new[]
	{
		WikiCompatibilityStatus.Ok, WikiCompatibilityStatus.Optional, WikiCompatibilityStatus.Unofficial, // OK
		WikiCompatibilityStatus.Broken, WikiCompatibilityStatus.Workaround, // broken
		WikiCompatibilityStatus.Abandoned, WikiCompatibilityStatus.Obsolete // abandoned
	}

	//.Except(new[] { WikiCompatibilityStatus.Abandoned, WikiCompatibilityStatus.Obsolete }) // if abandoned
	//.Except(new[] { WikiCompatibilityStatus.Broken, WikiCompatibilityStatus.Workaround }) // if broken
	.Except(new[] { WikiCompatibilityStatus.Ok, WikiCompatibilityStatus.Optional, WikiCompatibilityStatus.Unofficial }) // if OK
);

/// <summary>Whether to show data for the latest version of the game, even if it's a beta.</summary>
public bool ForBeta = true;

/// <summary>Whether to normalize mod folders.</summary>
public bool NormalizeFolders = true;

/// <summary>Whether to perform update checks for mods installed locally.</summary>
public bool UpdateCheckLocal = true;

/// <summary>Whether to show installed mods not found on the compatibility list.</summary>
public bool ShowMissingCompatMods = true;

/// <summary>Whether to show mods on the compatibility list that aren't installed locally. This should be false in most cases.</summary>
public bool ShowMissingLocalMods = false;

/// <summary>Whether to show potential errors in the compatibility list.</summary>
public bool ShowCompatListErrors = true;

/****
** Mod exception lists
****/
/// <summary>Mod IDs, update keys, custom URLs, or entry DLLs to ignore when checking if a local mod is on the wiki.</summary>
/// <remarks>This should only be used when a mod can't be cross-referenced because it has no ID and isn't released anywhere valid that can be used as an update key.</summary>
public string[] IgnoreMissingWikiMods = new[]
{
	// no ID
	"Nexus:450", // XmlSerializerRetool

	// Farm Automation
	"GitHub:oranisagu/SDV-FarmAutomation",
	"FarmAutomation.BarnDoorAutomation.dll",
	"FarmAutomation.ItemCollector.dll",

	// EvilPdor's mods
	"https://community.playstarbound.com/threads/111526",
	"RainRandomizer.dll",
	"StaminaRegen.dll",
	"WakeUp.dll",
	"WeatherController.dll",

	// local mods
	"Pathoschild.TestContentMod"
};

/// <summary>Mod IDs to ignore when checking if a wiki mod is installed locally.</summary>
public string[] IgnoreMissingLocalMods = new[]
{
	// bundled with SMAPI
	"SMAPI.ConsoleCommands",
	"SMAPI.SaveBackup",

	// no longer available for download
	"BALANCEMOD_AntiExhaustion", // Less Strict Over-Exertion by Permamiss: Nexus page hidden
	"ConfigurableShippingDates", // Configurable Shipping Dates by Nishtra: Nexus page hidden
	"Pathoschild.NoDebugMode"    // No Debug Mode by Pathoschild: Nexus page deleted
};

/// <summary>Maps mod IDs to the equivalent mod page URLs, in cases where that can't be determined from the mod data.</summary>
public IDictionary<string, string> OverrideModPageUrls = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
{
	// mods with only a forum thread ID
	["ANDR55.AdvMachines"] = "https://community.playstarbound.com/threads/137132", // Advanced Machines
	["AutoWater"] = "https://community.playstarbound.com/threads/129355",
	["FAKE.AshleyMod"] = "https://community.playstarbound.com/threads/112077",
	["FAKE.EvilPdor.WakeUp"] = "https://community.playstarbound.com/threads/111526",
	["FAKE.EvilPdor.StaminaRegen"] = "https://community.playstarbound.com/threads/111526",
	["FAKE.EvilPdor.WeatherController"] = "https://community.playstarbound.com/threads/111526",
	["FAKE.FarmAutomation.ItemCollector"] = "https://community.playstarbound.com/threads/111931",
	["FAKE.FarmAutomation.BarnDoorAutomation"] = "https://community.playstarbound.com/threads/111931",
	["FAKE.RainRandomizer"] = "https://community.playstarbound.com/threads/111526",
	["HappyAnimals"] = "https://community.playstarbound.com/threads/126655",
	["HorseWhistle_SMAPI"] = "https://community.playstarbound.com/threads/111550", // Horse Whistle (Nabuma)
	["KuroBear.SmartMod"] = "https://community.playstarbound.com/threads/108104",
	["RoyLi.Fireballs"] = "https://community.playstarbound.com/threads/129346",
	["RuyiLi.AutoCrop"] = "https://community.playstarbound.com/threads/129152",
	["RuyiLi.BloodTrail"] = "https://community.playstarbound.com/threads/129308",
	["RuyiLi.Emotes"] = "https://community.playstarbound.com/threads/129159",
	["RuyiLi.InstantFishing"] = "https://community.playstarbound.com/threads/129163",
	["RuyiLi.Kamikaze"] = "https://community.playstarbound.com/threads/129126",
	["RuyiLi.SlimeSpawner"] = "https://community.playstarbound.com/threads/129326",
	["Spouseroom"] = "https://community.playstarbound.com/threads/111636" // Spouse's Room Mod
};

/// <summary>Maps mod IDs to the folder name to use, overriding the name from the wiki.</summary>
public IDictionary<string, string> OverrideFolderNames = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
{
	// prefixes for testing convenience
	["bcmpinc.StardewHack"] = "@@Stardew Hack",
	["bwdyworks"] = "@Bwdyworks",
	["cat.Pong"] = "(1) Pong",
	["DIGUS.MailFrameworkMod"] = "@@Mail Framework Mod",
	["Entoarox.AdvancedLocationLoader"] = "@Advanced Location Loader",
	["Entoarox.EntoaroxFramework"] = "@Entoarox Framework",
	["Ilyaki.BattleRoyale"] = "(2) Battle Royalley",
	["Platonymous.ArcadePong"] = "(1) Arcade Pong",
	["Platonymous.Toolkit"] = "@@PyTK",
	["spacechase0.JsonAssets"] = "@@Json Assets",
	["spacechase0.SpaceCore"] = "@@SpaceCore",
	["TehPers.CoreMod"] = "@@Teh's Core Mod",

	// fix duplicate IDs (Slime Minigame)
	["Tofu.SlimeMinigame"] = "Slime Mods - Slime Minigame",
	["Tofu.SlimeQOL"] = "Slime Mods - SlimeQoL Alt",

	// fix ambiguous names
	["Vrakyas.CurrentLocation"] = "Current Location (Vrakyas)",
	["CurrentLocation102120161203"] = "Current Location (Omegasis)",

	["Thor.EnemyHealthBars"] = "Enemy Health Bars (TheThor59)",

	["HappyBirthday"] = "Happy Birthday (Oxyligen)",
	["Omegasis.HappyBirthday"] = "Happy Birthday (Omegasis)",

	["HorseWhistle_SMAPI"] = "Horse Whistle (Nabuma)",
	["icepuente.HorseWhistle"] = "Horse Whistle (Icepuente)",

	// prefix sub-mods
	["SilentOak.AutoQualityPatch"] = "Quality Products - Auto Quality Patch"
};

/// <summary>The mod versions to consider equivalent in update checks (indexed by mod ID to local/server versions).</summmary>
public IDictionary<string, Tuple<string, string>> EquivalentModVersions = new Dictionary<string, Tuple<string, string>>(StringComparer.InvariantCultureIgnoreCase)
{
	// broke in 1.2
	["439"] = Tuple.Create("1.2.1", "1.21.0"), // Almighty Tool
	["FileLoading"] = Tuple.Create("1.1.0", "1.12.0"), // File Loading
	["stephansstardewcrops"] = Tuple.Create("1.4.1", "1.41.0"), // Stephen's Stardew Crops

	// okay
	["alphablackwolf.skillPrestige"] = Tuple.Create("1.0.9-unofficial.1-huancz", "1.2.3"), // Skill Prestige (unofficial update is for an older version)
	["Jotser.AutoGrabberMod"] = Tuple.Create("1.0.12-beta.1", "1.0.12"),
	["skuldomg.freeDusty"] = Tuple.Create("1.0-beta.7", "1.0.5"), // Free Dusty
	["ElectroCrumpet.PelicanPostalService"] = Tuple.Create("1.0.5-beta", "1.0.6") // Pelican Postal Service
};

/*********
** Script
*********/
/// <summary>The data cache for expensive fetches.</summary>
private IBarrel Cache;

async Task Main()
{
	/****
	** Initialize
	****/
	Console.WriteLine("Initialising...");

	// cache
	Barrel.ApplicationId = this.CacheApplicationKey;
	this.Cache = Barrel.Current;
	this.Cache.EmptyExpired();
	//this.Cache.EmptyAll();

	// data
	var toolkit = new ModToolkit();
	var mods = new List<ModData>();

	// check tokens
	if (this.NormalizeFolders && File.Exists(this.ModFolderPathDoNotNormalizeToken))
	{
		Console.WriteLine("   WARNING: detected 'do not normalize' file, disabling folder normalising.");
		this.NormalizeFolders = false;
	}

	/****
	** Read local mod data
	****/
	Console.WriteLine("Reading local data...");
	foreach (ModFolder folder in toolkit.GetModFolders(this.ModFolderPath))
	{
		if (folder.Manifest == null)
		{
			Util.WithStyle($"   Ignored invalid mod: {folder.DisplayName} (manifest error: {folder.ManifestParseError})", "color: red;").Dump();
			continue;
		}
		mods.Add(new ModData(folder));
	}

	/****
	** Match to API data
	****/
	Console.WriteLine("Fetching API data...");
	{
		// get valid mods by ID
		IDictionary<string, ModData> fetchQueue = new Dictionary<string, ModData>(StringComparer.InvariantCultureIgnoreCase);
		{
			IDictionary<ModData, string> issues = new Dictionary<ModData, string>();
			int fakeIDs = 0;
			foreach (ModData mod in mods.Where(p => p.IsInstalled).OrderBy(p => p.GetDisplayName()))
			{
				// get unique ID
				string id = mod.Folder.Manifest.UniqueID;
				if (string.IsNullOrWhiteSpace(id))
				{
					id = $"SMAPI.FAKE.{fakeIDs++}";
					issues[mod] = $"has no ID (will use '{id}')";
				}

				// validate
				if (fetchQueue.ContainsKey(id))
				{
					issues[mod] = $"can't fetch API data (same ID as {fetchQueue[id].GetDisplayName()})";
					continue;
				}

				// can check for updates
				fetchQueue.Add(id, mod);
			}

			// show update-check issues
			if (issues.Any())
			{
				(
					from pair in issues
					let mod = pair.Key
					let issue = pair.Value
					let name = mod.GetDisplayName()
					orderby name
					select new
					{
						DisplayName = name,
						Folder = mod.Folder.DisplayName,
						Issue = issue
					}
				).Dump("found update-check issues for some mods");
			}
		}

		// fetch API data
		if (fetchQueue.Any())
		{
			IncrementalProgressBar progress = new IncrementalProgressBar(fetchQueue.Count) { HideWhenCompleted = true }.Dump();

			ISemanticVersion apiVersion = new SemanticVersion("3.0");
			WebApiClient client = new WebApiClient("http://api.smapi.io/", apiVersion);
			foreach (var pair in fetchQueue)
			{
				string id = pair.Key;
				var mod = pair.Value;

				progress.Increment();

				// fetch data
				Platform platform = EnvironmentUtility.DetectPlatform();
				ModSearchEntryModel searchModel = new ModSearchEntryModel(id, apiVersion, mod.UpdateKeys, isBroken: true); // isBroken ensures unofficial updates always listed
				string cacheKey = $"{id}|{string.Join(", ", mod.UpdateKeys)}|{this.ForBeta}";
				ModEntryModel result = this.CacheOrFetch(cacheKey, () => client.GetModInfo(new[] { searchModel }, apiVersion, gameVersion: null, platform, includeExtendedMetadata: true).Select(p => p.Value).FirstOrDefault());

				// select latest version
				mod.ApiRecord = result;
			}
		}
	}

	/****
	** Normalize mod folders
	****/
	if (this.NormalizeFolders)
	{
		Console.WriteLine("Normalising mod folders...");
		foreach (ModData mod in mods)
		{
			// get mod info
			if (!mod.IsInstalled)
				continue;
			ModFolder folder = mod.Folder;
			DirectoryInfo actualDir = folder.Directory;
			string searchFolderName = PathUtilities
				.GetSegments(folder.Directory.FullName)
				.Skip(PathUtilities.GetSegments(this.ModFolderPath).Length)
				.First(); // the name of the folder immediately under Mods containing this mod
			DirectoryInfo searchDir = new DirectoryInfo(Path.Combine(this.ModFolderPath, searchFolderName));

			// get page url
			string url = mod.GetModPageUrl();
			foreach (string id in mod.IDs)
			{
				if (this.OverrideModPageUrls.TryGetValue(id, out string @override))
				{
					url = @override;
					break;
				}
			}

			// create metadata file
			File.WriteAllText(
				Path.Combine(actualDir.FullName, "_metadata.txt"),
				$"page URL: {url}\n"
				+ $"IDs: {string.Join(", ", mod.IDs)}\n"
				+ $"update keys: {string.Join(", ", mod.UpdateKeys)}\n"
			);

			// normalize
			string newName = null;
			try
			{
				// get preferred name
				newName = mod.GetRecommendedFolderName();
				foreach (string id in mod.IDs)
				{
					if (this.OverrideFolderNames.TryGetValue(id, out string @override))
					{
						newName = @override;
						break;
					}
				}

				// mark unofficial versions
				if (mod.InstalledVersion.IsPrerelease() && (mod.InstalledVersion.PrereleaseTag.Contains("unofficial") || mod.InstalledVersion.PrereleaseTag.Contains("update")))
					newName += $" [unofficial]";

				// sanitize name
				foreach (char ch in Path.GetInvalidFileNameChars())
					newName = newName.Replace(ch, '_');

				// move to new name
				DirectoryInfo newDir = new DirectoryInfo(Path.Combine(this.ModFolderPath, newName));
				newDir.Parent.Create();
				if (actualDir.FullName != newDir.FullName)
				{
					Console.WriteLine($"   Moving {PathUtilities.GetRelativePath(this.ModFolderPath, actualDir.FullName)} to {PathUtilities.GetRelativePath(this.ModFolderPath, newDir.FullName)}...");
					if (newDir.Exists)
					{
						actualDir.MoveTo(newDir.FullName + "__TEMP");
						FileUtilities.ForceDelete(newDir);
					}
					actualDir.MoveTo(newDir.FullName);
					if (Directory.Exists(searchDir.FullName) && !searchDir.EnumerateFileSystemInfos().Any())
						FileUtilities.ForceDelete(searchDir);
				}
			}
			catch (Exception error)
			{
				new { error, newName, mod }.Dump("error normalising mod folder");
			}
		}
	}

	/****
	** Highlight potential compat list errors
	****/
	if (this.ShowCompatListErrors)
	{
		Console.WriteLine("Checking for potential compatibility list errors...");
		bool EqualsInvariant(string actual, string expected)
		{
			if (actual == null)
				return expected == null;
			return actual.Equals(expected, StringComparison.InvariantCultureIgnoreCase);
		}

		object Format(string actual, string expected)
		{
			return Util.WithStyle(
				actual ?? "null",
				EqualsInvariant(actual, expected) ? "color: green;" : "color: red; font-weight: bold;"
			);
		}

		var result = (
			from mod in mods
			let metadata = mod?.ApiRecord?.Metadata
			where metadata != null

			orderby (metadata?.Name ?? mod.Folder.DisplayName).Replace(" ", "").Replace("'", "")
			let compatHasID = metadata.ID.Any() == true
			let ids = mod.IDs.ToArray()
			let modHasID = ids.Any()
			let commonID = metadata.ID.Intersect(ids).FirstOrDefault()
			where
				((modHasID || compatHasID) && commonID == null)
				|| !EqualsInvariant(metadata.NexusID?.ToString(), mod.GetModID("Nexus", mustBeInt: true))
				|| !EqualsInvariant(metadata.ChucklefishID?.ToString(), mod.GetModID("Chucklefish", mustBeInt: true))
				|| !EqualsInvariant(metadata.GitHubRepo, mod.GetModID("GitHub"))
			select new
			{
				ID = ids.FirstOrDefault(),
				Wiki = metadata?.Name != null
					? new
					{
						Name = metadata.Name,
						ID = Format(commonID ?? (compatHasID ? string.Join(", ", metadata.ID) : null), commonID ?? (modHasID ? string.Join(", ", ids) : null)),
						NexusID = Format(metadata.NexusID?.ToString(), mod.GetModID("Nexus", mustBeInt: true)),
						ChucklefishID = Format(metadata.ChucklefishID?.ToString(), mod.GetModID("Chucklefish", mustBeInt: true)),
						GitHub = Format(metadata.GitHubRepo, mod.GetModID("GitHub"))
					}
					: (object)Format("not on wiki", null),
				UpdateKeys = string.Join(", ", mod.UpdateKeys),
				Raw = new Lazy<ModData>(() => mod)
			}
		);
		if (result.Any())
			result.Dump("Potential compatibility list errors");
	}

	/****
	** Report mods missing from the wiki
	****/
	if (this.ShowMissingCompatMods)
	{
		ModData[] missing =
			(
				from mod in mods
				where
					mod.ApiRecord?.Metadata == null
					&& mod.IsInstalled
					&& mod.Folder.Manifest.ContentPackFor == null
					&& !this.IgnoreMissingWikiMods.Intersect(mod.UpdateKeys).Any()
					&& !this.IgnoreMissingWikiMods.Intersect(mod.IDs).Any()
					&& !this.IgnoreMissingWikiMods.Contains(mod.Folder.Manifest.EntryDll)
				select mod
			)
			.Where(p => p.IDs.Count() > 1 || !p.IDs.FirstOrDefault()?.Contains("FAKE.") == true)
			.ToArray();
		if (missing.Any())
		{
			missing
				.Select(mod => new
				{
					FolderName = mod.Folder.Directory.Name,
					Manifest = new Lazy<IManifest>(() => mod.Folder.Manifest),
					RawManifest = new Lazy<string>(() => File.ReadAllText(Path.Combine(mod.Folder.Directory.FullName, "manifest.json"))),
					ApiRecord = new Lazy<ModEntryModel>(() => mod.ApiRecord),
					IDs = mod.IDs,
					UpdateKeys = mod.UpdateKeys,
					Installed = mod.InstalledVersion.ToString(),
					Source = mod.GetSourceUrl() != null ? new Hyperlinq(mod.GetSourceUrl(), "source") : null
				})
				.Dump("Installed mods not on compatibility list");
		}
	}

	/****
	** Report mods on the wiki not installed locally
	****/
	if (this.ShowMissingLocalMods)
	{
		// get mods installed locally
		ISet<string> localIds = new HashSet<string>(
			mods.Select(p => p.Folder.Manifest.UniqueID),
			StringComparer.InvariantCultureIgnoreCase
		);

		// fetch mods on the wiki that aren't installed
		WikiModList compatList = await toolkit.GetWikiCompatibilityListAsync();
		var missing =
			(
				from mod in compatList.Mods
				where
					// has an ID
					mod.ID.Any()
					&& !mod.ID.All(p => p == "none")

					// isn't ignored
					&& !this.IgnoreMissingLocalMods.Intersect(mod.ID).Any()

					// isn't a content pack
					&& mod.ContentPackFor == null

					// isn't installed locally
					&& !mod.ID.Any(id => localIds.Contains(id))
				
				let links = this.GetReportLinks(mod)

				select new
				{
					Name = mod.Name.FirstOrDefault(),
					Author = mod.Author.FirstOrDefault(),
					Nexus = links.Nexus,
					ModDrop = links.ModDrop,
					CurseForge = links.CurseForge,
					Chucklefish = links.Chucklefish,
					GitHub = links.GitHub,
					Custom = links.Custom,
					ID = mod.ID
				}
			)
			.OrderBy(p => p.Name, StringComparer.InvariantCultureIgnoreCase)
			.ToArray();
		
		if (missing.Any())
			missing.Dump("Mods on compatibility list not installed locally");
	}

	/****
	** Show final report
	****/
	this
		.GetReport(mods.Where(p => p.IsInstalled), this.ForBeta)
		.OrderByDescending(mod => mod.HasUpdate) // show mods with updated versions first
		.ThenBy(mod => mod.Name)
		.Select(mod =>
		{
			if (!mod.IsValid)
				return (dynamic)new { NormalizedFolder = Util.WithStyle(mod.NormalizedFolder, "color: red;") };

			const string smallStyle = "font-size: 0.8em;";
			const string errorStyle = "color: red; font-weight: bold;";
			const string fadedStyle = "color: gray;";

			// get mod info
			bool highlightStatus = mod.WikiStatus != null && this.HighlightStatuses.Contains(mod.WikiStatus.Value);
			string[] majorUpdateCheckErrors = mod.UpdateCheckErrors.Where(p => !p.Contains("matches a mod with invalid semantic version")).ToArray();
			string[] minorUpdateCheckErrors = mod.UpdateCheckErrors.Except(majorUpdateCheckErrors).ToArray();
			var apiMetadata = mod.ModData.ApiRecord?.Metadata;

			// issues to highlight
			List<string> issues = new List<string>();
			if (mod.WikiStatus == null)
				issues.Add("not on wiki");
			if (mod.Installed != null && mod.Latest != null && new SemanticVersion(mod.Latest).IsOlderThan(mod.Installed))
				issues.Add("official version is older");
			if (mod.Installed != null && mod.WikiUnofficialVersion != null && mod.WikiUnofficialVersion.IsOlderThan(mod.Installed))
				issues.Add("unofficial version on wiki is older");
			if (string.IsNullOrWhiteSpace(mod.UpdateKeys))
				issues.Add("no valid update keys in manifest or wiki");
			if (apiMetadata?.MapLocalVersions != null && apiMetadata.MapLocalVersions.Any(p => new SemanticVersion(p.Key).CompareTo(new SemanticVersion(mod.Installed)) == 0))
				issues.Add($"wiki maps local versions which don't match installed version.");
			if (apiMetadata?.MapRemoteVersions != null && apiMetadata.MapRemoteVersions.Any(p => new SemanticVersion(p.Key).IsOlderThan(mod.Latest)))
				issues.Add($"wiki maps remote versions older than the latest available version.");
			issues.AddRange(majorUpdateCheckErrors);

			// warnings
			List<string> minorWarnings = new List<string>();
			minorWarnings.AddRange(minorUpdateCheckErrors);

			// format version
			string versionHtml;
			if (mod.Latest == null)
				versionHtml = $"<span style='{smallStyle} {errorStyle}'>not found</span>";
			else if (mod.HasUpdate)
				versionHtml = $"<a href='{mod.DownloadUrl}' style='{smallStyle}'>{mod.Latest}</a>";
			else
				versionHtml = $"<span style='{smallStyle} {fadedStyle}'>{mod.Latest}</span>";


			return new
			{
				Name = Util.WithStyle(mod.Author != null ? $"{mod.Name}\n  by {mod.Author}" : mod.Name, smallStyle),
				Installed = Util.WithStyle(mod.Installed, smallStyle),
				Latest = Util.RawHtml(versionHtml),
				Status = Util.WithStyle(mod.WikiStatus, $"{smallStyle} {(highlightStatus ? errorStyle : "")}"),
				Summary = Util.WithStyle($"{mod.WikiSummary} {(!string.IsNullOrWhiteSpace(mod.WikiBrokeIn) ? $"[broke in {mod.WikiBrokeIn}]" : "")}".Trim(), $"{smallStyle} {(highlightStatus ? errorStyle : "")}"),
				Issues = (issues.Any()
					? Util.WithStyle("⚠ " + string.Join("\n⚠ ", issues), $"{smallStyle} {errorStyle}")
					: ""
				),
				MinorWarnings = (minorWarnings.Any()
					? Util.WithStyle("⚠ " + string.Join("\n⚠ ", minorWarnings), $"{smallStyle} {fadedStyle}")
					: ""
				),
				UpdateKeys = new Lazy<string>(() => mod.UpdateKeys),
				ManifestUpdateKeys = new Lazy<object>(() => mod.ManifestUpdateKeys != null ? mod.ManifestUpdateKeys : Util.WithStyle("none", errorStyle)),
				NormalizedFolder = new Lazy<string>(() => mod.NormalizedFolder),
				Manifest = new Lazy<Manifest>(() => mod.Manifest),
				Source = mod.SourceUrl != null ? new Hyperlinq(mod.SourceUrl, "source") : null,
				Links = new Lazy<object>(() => mod.Links),
				VersionMaps = apiMetadata?.MapLocalVersions?.Any() == true || apiMetadata?.MapRemoteVersions?.Any() == true
					? new Lazy<object>(() => new { apiMetadata.MapLocalVersions, apiMetadata.MapRemoteVersions })
					: null
			};
		})
		.Dump("mods");
}

/*********
** Helpers
*********/
/// <summary>Get links for a mod.</summary>
/// <param name="mod">The wiki entry for the mod.</param>
private dynamic GetReportLinks(WikiModEntry mod)
{
	if (mod == null)
		return null;
	
	return new
	{
		Nexus = mod.NexusID.HasValue ? new Hyperlinq($"https://www.nexusmods.com/stardewvalley/mods/{mod.NexusID}", $"Nexus:{mod.NexusID}") : null,
		ModDrop = mod.ModDropID.HasValue ? new Hyperlinq($"https://www.moddrop.com/sdv/mod/{mod.ModDropID}", $"ModDrop:{mod.ModDropID}") : null,
		CurseForge = mod.CurseForgeID.HasValue ? new Hyperlinq($"https://stardewvalley.curseforge.com/projects/{mod.CurseForgeKey}", $"CurseForge:{mod.CurseForgeKey}") : null,
		Chucklefish = mod.ChucklefishID.HasValue ? new Hyperlinq($"https://community.playstarbound.com/resources/{mod.ChucklefishID}", $"Chucklefish:{mod.ChucklefishID}") : null,
		GitHub = mod.GitHubRepo != null ? new Hyperlinq($"https://github.com/{mod.GitHubRepo}", $"GitHub:{mod.GitHubRepo}") : null,
		Custom = mod.CustomUrl != null ? new Hyperlinq(mod.CustomUrl, "custom") : null,
	};
}

/// <summary>Get links for a mod.</summary>
/// <param name="mod">The API metadata for the mod.</param>
private dynamic GetReportLinks(ModEntryModel mod)
{
	if (mod?.Metadata == null)
		return null;

	var meta = mod.Metadata;
	return new
	{
		Nexus = meta.NexusID.HasValue ? new Hyperlinq($"https://www.nexusmods.com/stardewvalley/mods/{meta.NexusID}", $"Nexus:{meta.NexusID}") : null,
		ModDrop = meta.ModDropID.HasValue ? new Hyperlinq($"https://www.moddrop.com/sdv/mod/{meta.ModDropID}", $"ModDrop:{meta.ModDropID}") : null,
		CurseForge = meta.CurseForgeID.HasValue ? new Hyperlinq($"https://stardewvalley.curseforge.com/projects/{meta.CurseForgeKey}", $"CurseForge:{meta.CurseForgeKey}") : null,
		Chucklefish = meta.ChucklefishID.HasValue ? new Hyperlinq($"https://community.playstarbound.com/resources/{meta.ChucklefishID}", $"Chucklefish:{meta.ChucklefishID}") : null,
		GitHub = meta.GitHubRepo != null ? new Hyperlinq($"https://github.com/{meta.GitHubRepo}", $"GitHub:{meta.GitHubRepo}") : null,
		Custom = meta.CustomUrl != null ? new Hyperlinq(meta.CustomUrl, "custom") : null,
	};
}

/// <summary>Get a flattened view of the mod data.</summary>
/// <param name="mods">The mods to represent.</param>
/// <param name="forBeta">Whether to render data for the beta version of Stardew Valley (if any).</param>
IEnumerable<ReportEntry> GetReport(IEnumerable<ModData> mods, bool forBeta)
{
	foreach (ModData mod in mods)
	{
		// not installed locally
		if (mod.Folder == null)
			continue;

		// yield info
		{
			// get latest version + URL
			var downloads = new[]
			{
				new { Version = mod.ApiRecord?.Metadata?.Main?.Version, Url = mod.ApiRecord?.Metadata?.Main?.Url },
				new { Version = mod.ApiRecord?.Metadata.Optional?.Version, Url = mod.ApiRecord?.Metadata?.Optional?.Url },
				new { Version = mod.GetUnofficialVersion(forBeta), Url = $"https://stardewvalleywiki.com/Modding:SMAPI_compatibility#{this.GetAnchor(mod.ApiRecord?.Metadata?.Name ?? mod.Folder.DisplayName)}" }
			};
			ISemanticVersion latestVersion = null;
			string downloadUrl = null;
			foreach (var download in downloads)
			{
				if (download.Version == null)
					continue;

				if (latestVersion == null || download.Version.IsNewerThan(latestVersion))
				{
					latestVersion = download.Version;
					downloadUrl = download.Url;
				}
			}

			// override update check
			bool ignoreUpdate = false;
			if (latestVersion != null && latestVersion != mod.InstalledVersion)
			{
				foreach (string id in mod.IDs)
				{
					if (this.EquivalentModVersions.TryGetValue(id, out Tuple<string, string> pair))
					{
						if (mod.InstalledVersion.ToString() == pair.Item1 && latestVersion.ToString() == pair.Item2)
						{
							ignoreUpdate = true;
							break;
						}
					}
				}
			}

			// build model
			yield return new ReportEntry(mod, latestVersion, downloadUrl, forBeta, ignoreUpdate, this.GetReportLinks(mod.ApiRecord));
		}
	}
}

/// <summary>Read data from the cache, or fetch and cache it.</summary>
/// <param name="key">The cache key.</param>
/// <param name="fetch">The method which fetches fresh data.</param>
T CacheOrFetch<T>(string key, Func<T> fetch)
{
	var jsonHelper = new JsonHelper();
	if (this.Cache.Exists(key))
	{
		string json = this.Cache.Get<string>(key);
		return jsonHelper.Deserialize<T>(json);
	}
	else
	{
		T data = fetch();
		string json = jsonHelper.Serialize(data); // MonkeyCache handles string values weirdly, and will try to deserialize as JSON when we read it
		this.Cache.Add(key, json, this.CacheTime);
		return data;
	}
}

/// <summary>The aggregated data for a mod.</summary>
class ModData
{
	/*********
	** Properties
	*********/
	/// <summary>The record from SMAPI's web API.</summary>
	private ModEntryModel ApiRecordImpl;


	/*********
	** Accessors
	*********/
	/// <summary>The mod metadata read from its folder.</summary>
	public ModFolder Folder { get; }

	/// <summary>The record from SMAPI's web API.</summary>
	public ModEntryModel ApiRecord
	{
		get { return this.ApiRecordImpl; }
		set
		{
			this.ApiRecordImpl = value;
			this.Update();
		}
	}

	/// <summary>The unique mod IDs.</summary>
	public string[] IDs { get; private set; }

	/// <summary>The aggregate update keys.</summary>
	public string[] UpdateKeys { get; private set; }

	/// <summary>Whether the mod is installed (regardless of whether it's compatible).</summary>
	public bool IsInstalled => this.Folder?.Manifest != null;

	/// <summary>The installed mod version.</summary>
	public ISemanticVersion InstalledVersion => this.Folder?.Manifest?.Version;


	/*********
	** Public methods
	*********/
	/// <summary>Construct an instance.</summary>
	/// <param name="folder">The mod metadata read from its folder.</param>
	public ModData(ModFolder folder)
	{
		this.Folder = folder;
		this.Update();
	}

	/// <summary>Get the mod ID in a repository from the mod's update keys, if available.</summary>
	/// <param name="repositoryKey">The case-insensitive repository key (like Nexus or Chucklefish) to match.</summary>
	/// <param name="mustBeInt">Whether the mod must be an integer value.</param>
	/// <returns>Retudns the mod ID, or <c>null</c> if not found.</returns>
	public string GetModID(string repositoryKey, bool mustBeInt = false)
	{
		foreach (string key in this.UpdateKeys.Where(p => p != null && p.StartsWith($"{repositoryKey}:", StringComparison.InvariantCultureIgnoreCase)))
		{
			string[] parts = key.Split(new[] { ':' }, 2);
			if (parts[1].Length <= 0)
				continue;
			
			if (mustBeInt)
			{
				if (int.TryParse(parts[1], out int id))
					return id.ToString(CultureInfo.InvariantCulture);
				continue;
			}
			
			return parts[1];
		}
		
		return null;
	}

	/// <summary>Get a display name for this mod.</summary>
	public string GetDisplayName()
	{
		return
			this.Folder?.DisplayName
			?? this.ApiRecord?.Metadata?.Name
			?? this.IDs.FirstOrDefault();
	}

	/// <summary>Get the unofficial update for this mod, if any.</summary>
	/// <param name="forBeta">If there's an ongoing Stardew Valley or SMAPI beta which affects compatibility, whether to return the unofficial update for that beta version instead of the one for the stable version.</param>
	public ISemanticVersion GetUnofficialVersion(bool forBeta)
	{
		return forBeta
			? this.ApiRecord?.Metadata?.UnofficialForBeta?.Version ?? this.ApiRecord?.Metadata?.Unofficial?.Version
			: this.ApiRecord?.Metadata.Unofficial?.Version;
	}

	/// <summary>Get the URL to this mod's web page.</summary>
	public string GetModPageUrl()
	{
		// Nexus ID
		string nexusID = this.GetModID("Nexus", mustBeInt: true);
		if (nexusID != null)
			return $"https://www.nexusmods.com/stardewvalley/mods/{nexusID}";

		// ModDrop ID
		string modDropId = this.GetModID("ModDrop", mustBeInt: true);
		if (modDropId != null)
			return $"https://www.moddrop.com/stardew-valley/mod/{modDropId}";

		// CurseForge key
		if (this.ApiRecord?.Metadata?.CurseForgeKey != null)
			return $"https://stardewvalley.curseforge.com/projects/{this.ApiRecord.Metadata.CurseForgeKey}";

		// Chucklefish ID
		string chucklefishID = this.GetModID("Chucklefish", mustBeInt: true);
		if (chucklefishID != null)
			return $"https://community.playstarbound.com/resources/{chucklefishID}";

		// GitHub key
		string repo = this.GetModID("GitHub");
		if (repo != null)
			return $"https://github.com/{repo}";

		return null;
	}

	/// <summary>Get the URL to the mod's code repository, if any.</summary>
	public string GetSourceUrl()
	{
		// GitHub
		string repo = this.GetModID("GitHub");
		if (repo != null)
			return $"https://github.com/{repo}";

		// custom source
		if (!string.IsNullOrWhiteSpace(this.ApiRecord?.Metadata?.CustomSourceUrl))
			return this.ApiRecord.Metadata.CustomSourceUrl;

		return null;
	}

	/// <summary>Get a recommended folder name based on the mod data.</summary>
	public string GetRecommendedFolderName()
	{
		return this.ApiRecord?.Metadata?.Name ?? this.Folder.DisplayName;
	}


	/*********
	** Private methods
	*********/
	/// <summary>Update the aggregated data.</summary>
	private void Update()
	{
		this.IDs = this.GetIDs().ToArray();
		this.UpdateKeys = this.GetUpdateKeys().ToArray();
	}

	/// <summary>Get the possible mod IDs.</summary>
	private IEnumerable<string> GetIDs()
	{
		IEnumerable<string> GetAll()
		{
			yield return this.Folder?.Manifest?.UniqueID;
			foreach (string cur in this.ApiRecord?.Metadata?.ID ?? new string[0])
				yield return cur.Trim();
		}

		return GetAll()
			.Where(p => !string.IsNullOrWhiteSpace(p))
			.Select(p => p.Trim())
			.Distinct();
	}

	/// <summary>Get the known update keys.</summary>
	private IEnumerable<string> GetUpdateKeys()
	{
		// get defined
		IEnumerable<string> GetDefined()
		{
			// mod folder
			var manifestKeys = this.Folder?.Manifest?.UpdateKeys;
			if (manifestKeys != null)
			{
				foreach (string key in manifestKeys)
				{
					if (!string.IsNullOrWhiteSpace(key))
						yield return key;
				}
			}

			// API record
			var compat = this.ApiRecord?.Metadata;
			if (compat != null)
			{
				foreach (string key in compat.GetUpdateKeys())
					yield return key;
			}
		}

		// yield uniques or default
		HashSet<string> seen = new HashSet<string>();
		foreach (string key in GetDefined())
		{
			UpdateKey parsed = UpdateKey.Parse(key);
			if (!parsed.LooksValid)
				continue;

			string parsedStr = parsed.ToString();
			if (seen.Add(parsedStr))
				yield return parsedStr;
		}
	}
}

/// <summary>An entry in the generated report.</summary>
class ReportEntry
{
	/********
	** Accessors
	********/
	/// <summary>The underlying mod data.</summary>
	public ModData ModData { get; }

	/// <summary>Whether the mod is correctly installed.</summary>
	public bool IsValid { get; }

	/// <summary>The mod manifest.</summary>
	public Manifest Manifest { get; }

	/// <summary>The mod folder name.</summary>
	public string NormalizedFolder { get; }

	/// <summary>The mod name.</summary>
	public string Name { get; }

	/// <summary>The mod author's name.</summary>
	public string Author { get; }

	/// <summary>The installed mod version.</summary>
	public string Installed { get; }

	/// <summary>The latest available mod version.</summary>
	public string Latest { get; }

	/// <summary>A comma-delimited list of update keys in the manifest file.</summary>
	public string ManifestUpdateKeys { get; }

	/// <summary>A comma-delimited list of update keys from all sources.</summary>
	public string UpdateKeys { get; }

	/// <summary>Whether a newer version than the one installed is available.</summary>
	public bool HasUpdate { get; }

	/// <summary>The compatibility status from the wiki.</summary>
	public WikiCompatibilityStatus? WikiStatus { get; }

	/// <summary>The compatibility 'broke in' field from the wiki.</summary>
	public string WikiBrokeIn { get; }

	/// <summary>The compatibility summary from the wiki.</summary>
	public string WikiSummary { get; }

	/// <summary>The unofficial version from the wiki, if any.</summary>
	public ISemanticVersion WikiUnofficialVersion { get; }

	/// <summary>The URL to download the latest version.</summary>
	public string DownloadUrl { get; set; }

	/// <summary>Any errors that occurred while checking for updates.</summary>
	public string[] UpdateCheckErrors { get; set; }

	/// <summary>The code repository URL, if any.</summary>
	public string SourceUrl { get; }
	
	/// <summary>An exportable list of links.</summary>
	public dynamic Links { get; }


	/********
	** Public methods
	********/
	public ReportEntry(ModData mod, ISemanticVersion latestVersion, string downloadUrl, bool forBeta, bool ignoreUpdate, dynamic links)
	{
		var manifest = mod.Folder.Manifest;
		var apiMetadata = mod.ApiRecord?.Metadata;

		this.ModData = mod;
		this.IsValid = true;
		this.Manifest = manifest;
		this.NormalizedFolder = mod.Folder.Directory.Name;
		this.Name = manifest.Name;
		this.Author = manifest.Author;
		this.Installed = manifest.Version.ToString();
		this.Latest = latestVersion?.ToString();
		this.ManifestUpdateKeys = manifest.UpdateKeys != null ? string.Join(", ", manifest.UpdateKeys) : null;
		this.UpdateKeys = string.Join(", ", mod.UpdateKeys);
		this.HasUpdate = !ignoreUpdate && latestVersion != null && latestVersion.IsNewerThan(mod.InstalledVersion);
		this.DownloadUrl = downloadUrl;
		this.UpdateCheckErrors = mod.ApiRecord?.Errors ?? new string[0];
		this.SourceUrl = mod.GetSourceUrl();
		this.Links = links;
		if (forBeta)
		{
			this.WikiStatus = apiMetadata?.BetaCompatibilityStatus ?? apiMetadata?.CompatibilityStatus;
			this.WikiSummary = apiMetadata?.BetaCompatibilitySummary ?? apiMetadata?.CompatibilitySummary;
			this.WikiBrokeIn = apiMetadata?.BetaBrokeIn ?? apiMetadata?.BrokeIn;
			this.WikiUnofficialVersion = apiMetadata?.UnofficialForBeta?.Version ?? apiMetadata?.Unofficial?.Version;
		}
		else
		{
			this.WikiStatus = apiMetadata?.CompatibilityStatus;
			this.WikiSummary = apiMetadata?.CompatibilitySummary;
			this.WikiBrokeIn = apiMetadata?.BrokeIn;
		}
	}
}

/// <summary>Get the unique anchor for the mod on the compatibility list, excluding the '#' symbol.</summary>
/// <param name="name">The standardized mod name.</param>
private string GetAnchor(string name)
{
	name = name.Replace(' ', '_');
	return WebUtility
		.UrlEncode(name)
		?.Replace('%', '.')
		.Replace("(", ".28")
		.Replace(")", ".29");
}