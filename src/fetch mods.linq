<Query Kind="Program">
  <Reference>C:\source\_Stardew\SMAPI\bin\Debug\SMAPI.Toolkit\netstandard2.0\SMAPI.Toolkit.CoreInterfaces.dll</Reference>
  <Reference>C:\source\_Stardew\SMAPI\bin\Debug\SMAPI.Toolkit\netstandard2.0\SMAPI.Toolkit.dll</Reference>
  <NuGetReference>HtmlAgilityPack</NuGetReference>
  <NuGetReference>Newtonsoft.Json</NuGetReference>
  <NuGetReference>Pathoschild.FluentNexus</NuGetReference>
  <NuGetReference>Squid-Box.SevenZipSharp</NuGetReference>
  <Namespace>Newtonsoft.Json</Namespace>
  <Namespace>Newtonsoft.Json.Converters</Namespace>
  <Namespace>Newtonsoft.Json.Linq</Namespace>
  <Namespace>Pathoschild.FluentNexus</Namespace>
  <Namespace>Pathoschild.FluentNexus.Models</Namespace>
  <Namespace>Pathoschild.Http.Client</Namespace>
  <Namespace>SevenZip</Namespace>
  <Namespace>StardewModdingAPI</Namespace>
  <Namespace>StardewModdingAPI.Toolkit</Namespace>
  <Namespace>StardewModdingAPI.Toolkit.Framework.Clients.Wiki</Namespace>
  <Namespace>StardewModdingAPI.Toolkit.Framework.ModScanning</Namespace>
  <Namespace>StardewModdingAPI.Toolkit.Serialization</Namespace>
  <Namespace>StardewModdingAPI.Toolkit.Serialization.Models</Namespace>
  <Namespace>System.Globalization</Namespace>
  <Namespace>System.Net</Namespace>
  <Namespace>System.Threading.Tasks</Namespace>
</Query>

/*

  This script...
     1. fetches metadata and files for every Stardew Valley mod on Nexus;
	 2. unpacks the downloaded files;
	 3. parses the unpacked downloads;
	 4. optionally runs custom queries over the metadata & downloads.

*/
#load "Utilities/ConsoleHelper.linq"
#load "Utilities/FileHelper.linq"
#load "Utilities/IncrementalProgressBar.linq"

/*********
** Configuration
*********/
/// <summary>The mod site clients from which to fetch mods.</summary>
readonly IModSiteClient[] ModSites = new IModSiteClient[]
{
	new NexusApiClient(
		apiKey: "",
		appName: "Pathoschild",
		appVersion: "1.0.0"
	)
};

/// <summary>The path in which to store cached data.</summary>
readonly string RootPath = @"D:\dev\mod-dump";

/// <summary>Which mods to refetch from the mod sites (or <c>null</c> to not refetch any).</summary>
readonly ISelectStrategy FetchMods =
	null;
	//new FetchAllFromStrategy(startFrom: 3792);
	//new FetchUpdatedStrategy(TimeSpan.FromDays(3));

/// <summary>Whether to delete the entire unpacked folder and unpack all files from the export path. If this is false, only updated mods will be re-unpacked.</summary>
readonly bool ResetUnpacked = false;

/// <summary>Nexus mod IDs to ignore when validating or cross-referencing mods.</summary>
readonly HashSet<int> IgnoreNexusIDsForValidation = new HashSet<int>
{
	// non-mod tools
	3431, // BFAV JSON Update [tool]
	4241, // Dreamy Valley Reshade
	1080, // Easy XNB for Xnb Node
	1213, // Natural Color - Reshade
	21,   // SDVMM/Stardew Valley Mod Manager
	1022, // SDV MultiTweak
	4429, // Separated layers for easy custom recoloring - For Gimp and Photoshop
	2400, // SMAPI
	2367, // SMAPI Templates [for Visual Studio]
	782,  // Sound Modding Tools
	1298, // Stardew Editor
	3814, // Stardew Valley Hack Player for Name_Yusuf (???)
	4536, // Stardew Valley Mod Manager 2
	3916, // Stardew Valley Money Hack
	3787, // Stardew Valley Planner
	2451, // StardewZem - Very Easy XNB Merger
	337,  // SVPM/Stardew Valley Package Manager
	1832, // Twelfth Night - American Gothic - ReShade
	1770, // Twelfth Night - Depixelate - ReShade
	1798, // Twelfth Night - Gameboy Pocket - ReShade
	2152, // Updated XACT file for audio modding [.xap file],

	// mod translations
	2825, // Auto-Grabber Mod (zh)
	4305, // Climates of Ferngill (pt)
	3954, // Happy Birthday (pt)
	4197, // Companion NPCs (pt)
	4339, // Lunar Disturbances (pt)
	4265, // Magic (pt)
	4370, // Trent's New Animals (pt)

	// mods which include a copy of another mod for some reason
	3496, // Farm Extended (content pack with a copy of Farm Type Manager)
	1692, // New NPC Alec (content pack with a copy of Custom Element Handler, Custom Farming, Custom Furniture, and Custom NPC)
	1128, // New Shirts and 2 new Skirts (includes Get Dressed)
	2426, // Unofficial Balance Patch (includes Artifact System Fixed, Better Quarry, Mining at the Farm, and Profession Adjustments)

	// reposts
	1765, // Console Commands
	1427, // Prairie King Made Easy
	887,  // Reseed
	1363, // Save Anywhere
	1077, // UI Mod Suite

	// special cases
	4109, // PPJA Home of Abandoned Mods - CFR Conversions
	4181  // Hilltop Immersive Farm (replaces a file in Immersive Farm 2)
};

/// <summary>Nexus file IDs to ignore when validating or cross-referencing mods.</summary>
readonly HashSet<int> IgnoreFileIDsForValidation = new HashSet<int>
{
	// pre-manifest SMAPI mods
	239,   // Rise and Shine (#3)
	294,   // Sprint (#2)
	456,   // Taxes Mod (#38)

	// SMAPI mods with outdated manifest formats (e.g. old version format)
	929,   // No Soil Decay (#283)
	2949,  // Siv's Marriage Mod (#366)
	3757,  // SmartMod (#1048)

	// replacement files (e.g. tbin to drop into downloaded mod)
	12282, // Ace's Expanded Farms MTN (#2711) > MelodicLullaby Less Saturated Valley Compatibility
	2051,  // Add a Room and Attic (#379)
	16992, // Bears in the Barn for BFAV (#4000) > BFAV JSON Update data file
	17704, // Better Woods (#3995) > Selective compatibility Immersive Farm 2
	17688, // BFAV Bulls (#4136) > BFAV JSON Update
	17685, // BFAV Cel's sheep (#3399) > Animals file to BFAV Json Update
	16979, // BFAV Cutter Animals (#4016) > Animals file to BFAV Json Update
	17296, // BFAV Dragons (#3991) > BFAV JSON Update
	17687, // BFAV Pokemons (#3396) > Animals file to BFAV Json Update
	17684, // BFAV More Blue Chickens (#3400) > file for BFAV Json Update
	17686, // BFAV Round Chickens mod (#3398) > Animals file to BFAV Json Update
	16975, // BFAV Velociraptors (#4015) > Animals file to BFAV Json Update
	9873,  // Even More Secret Woods (#2364), replacement file for Immersive Farm 2
	13120, // Immersive Farm 2 (#1531)
	13647, // Immersive Farm 2 (#1531)
	12863, // Secret Gardens Greenhouse (#3067) > "" for Immersive Farm 2
	17756, // Stardew Valley Reimagined (#4119) > compatibility patches
	17692, // Trent's New Animals (#3634) > JSON Update

	// legacy zipped Seasonal Immersion content packs
	5438,  // Seasonal Custom Farm Buildings (#1451)
	5439,  // Seasonal Custom Farm Buildings (#1451)
	3164,  // Seasonal Victorian Buildings and Flowers (#891)

	// legacy CustomNPC pack (files to drop into Mods/CustomNPC/Npcs)
	8179,  // Costum Npc Base (#1964)
	8203,  // Costum Npc Base (#1964)
	7569,  // CustomNPCs Nagito Komaeda (#1964)
	6423,  // NPC Alec (#1692)
	8870,  // Steins Gate Kurisu Maho and Leskinen mod (#2249)
	8871,  // Steins Gate Kurisu Maho and Leskinen mod (#2249)

	// legacy Stardew Symphony pack (files to drop into Mods/StardewSymphonyRemastered/Content/Music/Wav)
	12421, // Chill of Winter Music Pack (#3015)

	// Better Farm Animal Variety pack (files to merge into BFAV's config file)
	14395, // Gray Chicken (#3416)
	14394, // Harvest Moon Cows (#3419)
	14365, // Yoshis (#3420)
	14366, // Zelda LTTP Lifestock Animals (#3421)

	// collections of zipped content packs
	13533, // A Less Yellow Stardew (#2415) > All Lanuage Version In One File
	17433, // A Less Yellow Stardew (#2415) > ALYSD Map update (invalid manifest)
	9295,  // Clint Narrative Overhaul (#1067)
	9297,  // Demetrius Narrative Overhaul (#1120)
	9303,  // Dwarf Narrative Overhaul (#1250)
	9299,  // Gus Narrative Overhaul (#1144)
	9307,  // Linus Narrative Overhaul (#1488)
	9301,  // Marnie Narrative Overhaul (#1192)
	9309,  // Pam Narrative Overhaul (#1978)
	9293,  // Willy Narrative Overhaul (#1047)
	9305,  // Wizard Narrative Overhaul (#1309)

	// XNB mods with non-standard files
	9634,  // Ali's Foraging Map With a Few Changes (#2381), includes redundant .zip files
	445,   // Better Pigs and Recolours (#10), collection of zipped XNB mods
	2008,  // Chickens to Cardinal or Toucan (#578), XNB mod with misnamed `White Chickenxnb`
	10040, // Hero Academia Shota Mod (#2490), includes .zip file
	4462,  // Hope's Secret Cave (#1155), includes unpacked files
	535,   // New Rabbit Sprites and Recolours (#535), collection of zipped XNB mods
	2118,  // Semi-Realistic Animal Replacer (#597), collection of zipped XNB mods
	1680,  // Simple Building Cleaner (#493), has a `ModInfo.ini` file for some reason
	15332, // Tieba Chinese Revision (#2936), has junk files to show instructions in filenames
	2224,  // Toddlers Take After Parents (#626), files misnamed with `.zip_`

	// utility mods that are part of a larger mod
	14752, // Always On Server for Multiplayer (#2677) > Server Connection Reset
	9477,  // Even More Secret Woods (#2364) > Bush Reset
	3858,  // Hope's Farmer Customization Mods (#1008) > Hope's Character Customization Mods Improved [Demiacle.ExtraHair]
	14167, // Village Map Mod (#3355) > Village Console Commands

	// legacy/broken content packs
	7425,  // Earth and Water Obelisks (#1980) > Fahnestock - Seasonal Immersion
	7426,  // Earth and Water Obelisks (#1980) > Garrison - Seasonal Immersion
	7427,  // Earth and Water Obelisks (#1980) > Nantucket - Seasonal Immersion
	7428,  // Earth and Water Obelisks (#1980) > Rhinebeck - Seasonal Immersion
	7429,  // Earth and Water Obelisks (#1980) > Stonybrook - Seasonal Immersion
	7430,  // Earth and Water Obelisks (#1980) > Saratoga - Seasonal Immersion
	5534,  // Hudson Valley Buildings (#1478) > Fahnestock
	5531,  // Hudson Valley Buildings (#1478) > Garrison
	5532,  // Hudson Valley Buildings (#1478) > Nantucket
	5533,  // Hudson Valley Buildings (#1478) > Rhinebeck
	5530,  // Hudson Valley Buildings (#1478) > Saratoga
	5529,  // Hudson Valley Buildings (#1478) > Stonybrook
	10660, // katekatpixels Portrait Overhauls (#2602) > Content Patcher Version

	// other
	10976, // Always On Server (#2677) > AutoHotKey Paste Every 2 Minutes
	12257, // Always On Server (#2677) > Auto Restart SDV
	13516, // Battle Royalley (#3199) > World File for Hosting
	14839, // Battle Royalley (#3199), custom .bat/.command/.sh launch script
	15901, // Better Crab Pots (#3159) > Config Updater
	10352, // Birthstone Plants (#1632), JA pack with broken manifest JSON
	5721,  // Chao Replacement for Cat (#1524), .wav files
	15399, // Hidden Forest Farm (#3583) > XNB version, includes .tbin file
	14664, // Husky New NPC (#14664), has .xslx file in root with multiple content pack folders
	9967,  // Sam to Samantha (#2472), CP pack with invalid update keys
	18065, // Spouse Rooms Redesigned (#828) > All Options
	16623, // Stardew In-Game Daily Planner > Example Plan
	16660, // Stardew In-Game Daily Planner > Example Checklist
	18999, // Stardew Valley Expanded (#3753) > Wallpapers, Event Guide and Script
	11717, // Pencilstab's Portraits (#2351), content pack with separate previews folder including .zip
	9495,  // Quieter Cat Dog and Keg (#2371), .wav files
};

/// <summary>The settings to use when writing JSON files.</summary>
readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
{
	Formatting = Newtonsoft.Json.Formatting.Indented,
	Converters = new List<JsonConverter>
	{
		new StringEnumConverter() // save enums as their string names
	}
};


/*********
** Script
*********/
async Task Main()
{
	Directory.CreateDirectory(this.RootPath);

	// fetch mods
	HashSet<string> unpackMods = new HashSet<string>();
	if (this.FetchMods != null)
	{
		foreach (IModSiteClient modSite in this.ModSites)
		{
			int[] imported = await this.ImportMods(modSite, fetchStrategy: this.FetchMods, rootPath: this.RootPath);
			foreach (int id in imported)
				unpackMods.Add(Path.Combine(modSite.SiteKey.ToString(), id.ToString(CultureInfo.InvariantCulture)));
		}
	}

	// unpack fetched files
	HashSet<string> modFoldersToUnpack = new HashSet<string>(this.GetModFoldersWithFilesNotUnpacked(this.RootPath), StringComparer.InvariantCultureIgnoreCase);
	this.UnpackMods(rootPath: this.RootPath, filter: folder => this.ResetUnpacked || unpackMods.Any(p => folder.FullName.EndsWith(p)) || modFoldersToUnpack.Any(p => folder.FullName.EndsWith(p)));

	// run analysis
	ParsedMod[] mods = this.ReadMods(this.RootPath).ToArray();
	await this.GetModsNotOnWikiAsync(mods).Dump("SMAPI mods not on the wiki");
	this.GetInvalidMods(mods).Dump("Mods marked invalid by SMAPI toolkit (except blacklist)");
	this.GetInvalidIgnoreEntries(mods).Dump($"{nameof(IgnoreNexusIDsForValidation)}/{nameof(IgnoreFileIDsForValidation)} values which don't match any downloaded mod/file");
}


/*********
** Common queries
*********/
/// <summary>Get SMAPI mods which aren't listed on the wiki compatibility list.</summary>
/// <param name="mods">The mods to check.</param>
async Task<dynamic[]> GetModsNotOnWikiAsync(IEnumerable<ParsedMod> mods)
{
	// fetch mods on the wiki
	ModToolkit toolkit = new ModToolkit();
	WikiModList compatList = await toolkit.GetWikiCompatibilityListAsync();
	HashSet<string> knownModIDs = new HashSet<string>(compatList.Mods.SelectMany(p => p.ID), StringComparer.InvariantCultureIgnoreCase);
	HashSet<int> knownNexusIDs = new HashSet<int>(compatList.Mods.Where(p => p.NexusID.HasValue).Select(p => p.NexusID.Value));

	// fetch report
	return (
		from mod in mods
		from folder in mod.ModFolders
		where
			folder.ModType == ModType.Smapi
			&& !string.IsNullOrWhiteSpace(folder.ModID)
			&& (!knownModIDs.Contains(folder.ModID) || !knownNexusIDs.Contains(mod.ID))
			&& !this.ShouldIgnoreForValidation(mod.ID, folder.ID)
		let manifest = folder.RawFolder.Value.Manifest

		let names =
			(
				from name in new[] { folder.ModDisplayName?.Trim(), mod.Name?.Trim() }
				where !string.IsNullOrWhiteSpace(name)
				orderby name
				select name
			)
			.Distinct(StringComparer.InvariantCultureIgnoreCase).OrderBy(p => p)

		let authorNames =
			(
				from name in
					new[] { manifest?.Author?.Trim(), mod.Author?.Trim(), mod.AuthorLabel?.Trim() }
					.Where(p => p != null)
					.SelectMany(p => p.Split(','))
					.Select(p => p.Trim())
				where !string.IsNullOrWhiteSpace(name)
				orderby name
				select name
			)
			.Distinct(StringComparer.InvariantCultureIgnoreCase)

		select new
		{
			NexusID = new Hyperlinq(mod.PageUrl, mod.ID.ToString()),
			NexusName = mod.Name,
			NexusAuthor = mod.Author,
			NexusAuthorLabel = mod.AuthorLabel,
			NexusVersion = SemanticVersion.TryParse(mod.Version, out ISemanticVersion nexusVersion) ? nexusVersion.ToString() : mod.Version,
			folder.ID,
			folder.Type,
			folder.DisplayName,
			folder.ModType,
			folder.ModID,
			folder.ModVersion,
			UpdateKeys = new Lazy<string[]>(() => manifest.UpdateKeys),
			Manifest = new Lazy<Manifest>(() => manifest),
			NexusMod = new Lazy<ParsedMod>(() => mod),
			Folder = new Lazy<ParsedFile>(() => folder),
			WikiEntry = new Lazy<string>(() =>
				"{{/entry\n"
				+ $"  |name     = {string.Join(", ", names)}\n"
				+ $"  |author   = {string.Join(", ", authorNames)}\n"
				+ $"  |id       = {manifest?.UniqueID}\n"
				+ $"  |nexus id = {mod.ID}\n"
				+ $"  |github   = {manifest?.UpdateKeys?.Where(p => p.Trim().StartsWith("GitHub:")).Select(p => p.Trim().Substring("GitHub:".Length)).FirstOrDefault()}\n"
				+ "}}"
			)
		}
	)
	.OrderBy(p => p.NexusName)
	.ToArray();
}

/// <summary>Get mods which the SMAPI toolkit marked as invalid or unparseable.</summary>
/// <param name="mods">The mods to check.</param>
IEnumerable<dynamic> GetInvalidMods(IEnumerable<ParsedMod> mods)
{
	return (
		from mod in mods
		let invalid = mod.ModFolders
			.Where(folder =>
				(folder.ModType == ModType.Invalid || folder.ModType == ModType.Ignored)
				&& folder.ModError != ModParseError.EmptyFolder // contains only non-mod files (e.g. replacement PNG assets)
				&& !this.ShouldIgnoreForValidation(mod.ID, folder.ID)
			)
			.ToArray()
		where invalid.Any()
		select new { mod, invalid }
	)
	.ToArray();
}

/// <summary>Get entries in <see cref="IgnoreNexusIDsForValidation" /> or <see cref="IgnoreFileIDsForValidation" /> which don't match any of the given mods.</summary>
/// <param name="mods">The mods to check.</param>
IEnumerable<dynamic> GetInvalidIgnoreEntries(IEnumerable<ParsedMod> mods)
{
	HashSet<int> invalidModIds = new HashSet<int>(this.IgnoreNexusIDsForValidation);
	HashSet<int> invalidFileIds = new HashSet<int>(this.IgnoreFileIDsForValidation);

	foreach (ParsedMod mod in mods)
	{
		invalidModIds.Remove(mod.ID);
		foreach (var folder in mod.ModFolders)
			invalidFileIds.Remove(folder.ID);
	}

	foreach (int modId in invalidModIds)
		yield return new { Type = "mod id", modId };
	foreach (int fileId in invalidFileIds)
		yield return new { Type = "file id", fileId };
}

/// <summary>Get all mods which depend on the given mod.</summary>
/// <param name="parsedMods">The mods to check.</param>
/// <param name="modID">The dependency mod ID.</param>
IEnumerable<ModFolder> GetModsDependentOn(IEnumerable<ParsedMod> parsedMods, string modID)
{
	foreach (ParsedMod mod in parsedMods)
	{
		foreach (ModFolder folder in mod.ModFolders.Select(p => p.RawFolder.Value))
		{
			bool dependency =
				folder.Manifest?.Dependencies?.Any(p => p.UniqueID?.Equals(modID, StringComparison.InvariantCultureIgnoreCase) == true) == true
				|| folder.Manifest?.ContentPackFor?.UniqueID?.Equals(modID, StringComparison.InvariantCultureIgnoreCase) == true;
			if (dependency)
				yield return folder;
		}
	}
}

/*********
** Implementation
*********/
/// <summary>Import data for matching mods.</summary>
/// <param name="modSite">The mod site API client.</param>
/// <param name="fetchStrategy">The strategy which decides which mods to fetch.</param>
/// <param name="rootPath">The path in which to store cached data.</param>
/// <returns>Returns the imported mod IDs.</returns>
async Task<int[]> ImportMods(IModSiteClient modSite, ISelectStrategy fetchStrategy, string rootPath)
{
	// get mod IDs
	int[] modIDs = await fetchStrategy.GetModIds(modSite);
	if (!modIDs.Any())
		return modIDs;

	// fetch mods
	var progress = new IncrementalProgressBar(modIDs.Length).Dump();
	foreach (int id in modIDs)
	{
		// update progress
		progress.Increment();
		progress.Caption = $"Fetching mod {id} ({progress.Percent}%)";

		// fetch
		await this.ImportMod(modSite, id, fetchStrategy, rootPath);
	}

	progress.Caption = $"Fetched {modIDs.Length} updated mods ({progress.Percent}%)";
	return modIDs;
}

/// <summary>Import data for a given mod.</summary>
/// <param name="nexus">The Nexus API client.</param>
/// <param name="id">The unique mod ID.</param>
/// <param name="selectStrategy">The strategy which decides which mods to fetch.</param>
/// <param name="rootPath">The path in which to store cached data.</param>
async Task ImportMod(IModSiteClient modSite, int id, ISelectStrategy selectStrategy, string rootPath)
{
	while (true)
	{
		try
		{
			// fetch mod data
			GenericMod mod;
			try
			{
				mod = await modSite.GetModAsync(id);
			}
			catch (KeyNotFoundException)
			{
				ConsoleHelper.Print($"Skipped mod {id} (HTTP 404).", Severity.Warning);
				return;
			}
			catch (RateLimitedException ex)
			{
				TimeSpan unblockTime = ex.TimeUntilRetry;
				ConsoleHelper.Print($"Rate limit exhausted: {ex.RateLimitSummary}; resuming in {this.GetFormattedTime(unblockTime)} ({DateTime.Now + unblockTime} local time).");
				Thread.Sleep(unblockTime);
				continue;
			}

			// save to cache
			await this.DownloadAndCacheModDataAsync(modSite.SiteKey, mod, rootPath, getDownloadLinks: async file => await modSite.GetDownloadUrlsAsync(mod, file));
			break;
		}
		catch (Exception ex)
		{
			new { error = ex, response = await (ex as ApiException)?.Response?.AsString() }.Dump("error occurred");
			string choice = ConsoleHelper.GetChoice("What do you want to do?", "r", "s", "a");
			if (choice == "r")
				continue; // retry
			else if (choice == "s")
				return; // skip
			else if (choice == "a")
				throw; // abort
			else
				throw new NotSupportedException($"Invalid choice: '{choice}'", ex);
		}
	}
}

/// <summary>Write mod data to the cache directory and download the available files.</summary>
/// <param name="siteKey">The mod site from which to fetch.</param>
/// <param name="mod">The mod data to save.</param>
/// <param name="rootPath">The path in which to store cached data.</param>
/// <param name="getDownloadLinks">Get the download URLs for a specific file. If this returns multiple URLs, the first working one will be used.</param>
async Task DownloadAndCacheModDataAsync(ModSite siteKey, GenericMod mod, string rootPath, Func<GenericFile, Task<Uri[]>> getDownloadLinks)
{
	// reset cache folder
	DirectoryInfo folder = new DirectoryInfo(Path.Combine(rootPath, siteKey.ToString(), mod.ID.ToString(CultureInfo.InvariantCulture)));
	if (folder.Exists)
	{
		FileHelper.ForceDelete(folder);
		folder.Refresh();
	}
	folder.Create();
	folder.Refresh();

	// save mod info
	File.WriteAllText(Path.Combine(folder.FullName, "mod.json"), JsonConvert.SerializeObject(mod, this.JsonSettings));

	// save files
	using (WebClient downloader = new WebClient())
	{
		foreach (GenericFile file in mod.Files)
		{
			// create folder
			FileInfo localFile = new FileInfo(Path.Combine(folder.FullName, "files", $"{file.ID}{Path.GetExtension(file.FileName)}"));
			localFile.Directory.Create();

			// download file from first working CDN
			Queue<Uri> sources = new Queue<Uri>(await getDownloadLinks(file));
			while (true)
			{
				if (!sources.Any())
				{
					ConsoleHelper.Print($"Skipped file {file.ID} > {file.ID}: no download sources available for this file.", Severity.Error);
					break;
				}

				Uri url = sources.Dequeue();
				try
				{
					downloader.DownloadFile(url, localFile.FullName);
					break;
				}
				catch (Exception ex)
				{
					ConsoleHelper.Print($"Failed downloading mod {mod.ID} > file {file.ID} from {url}.{(sources.Any() ? " Trying next CDN..." : "")}\n{ex}", Severity.Error);
				}
			}
		}
	}
}

/// <summary>Get all mod folders which have files that haven't been unpacked.</summary>
/// <param name="rootPath">The path containing mod folders.</param>
IEnumerable<string> GetModFoldersWithFilesNotUnpacked(string rootPath)
{
	// unpack files
	foreach (DirectoryInfo siteDir in this.GetSortedSubfolders(new DirectoryInfo(rootPath)))
	{
		foreach (DirectoryInfo modDir in this.GetSortedSubfolders(siteDir))
		{
			// get packed folder
			DirectoryInfo packedDir = new DirectoryInfo(Path.Combine(modDir.FullName, "files"));
			if (!packedDir.Exists)
				continue;

			// check for files that need unpacking
			DirectoryInfo unpackedDir = new DirectoryInfo(Path.Combine(modDir.FullName, "unpacked"));
			foreach (FileInfo archiveFile in packedDir.GetFiles())
			{
				if (archiveFile.Extension == ".exe")
					continue;

				string id = Path.GetFileNameWithoutExtension(archiveFile.Name);
				DirectoryInfo targetDir = new DirectoryInfo(Path.Combine(unpackedDir.FullName, id));
				if (!targetDir.Exists)
				{
					yield return Path.Combine(siteDir.Name, modDir.Name);
					break;
				}
			}
		}
	}
}

/// <summary>Unpack all mods in the given folder.</summary>
/// <param name="rootPath">The path in which to store cached data.</param>
/// <param name="filter">A filter which indicates whether a mod folder should be unpacked.</param>
void UnpackMods(string rootPath, Func<DirectoryInfo, bool> filter)
{
	SevenZipBase.SetLibraryPath(Environment.Is64BitOperatingSystem && !Environment.Is64BitProcess
		? @"C:\Program Files (x86)\7-Zip\7z.dll"
		: @"C:\Program Files\7-Zip\7z.dll"
	);

	foreach (DirectoryInfo siteDir in new DirectoryInfo(rootPath).EnumerateDirectories())
	{
		// get folders to unpack
		DirectoryInfo[] modDirs = this
			.GetSortedSubfolders(siteDir)
			.Where(filter)
			.ToArray();
		if (!modDirs.Any())
			return;

		// unpack files
		var progress = new IncrementalProgressBar(modDirs.Count()).Dump();
		foreach (DirectoryInfo modDir in modDirs)
		{
			progress.Increment();
			progress.Caption = $"Unpacking {siteDir.Name} > {modDir.Name} ({progress.Percent}%)...";

			// get packed folder
			DirectoryInfo packedDir = new DirectoryInfo(Path.Combine(modDir.FullName, "files"));
			if (!packedDir.Exists)
				continue;

			// create/reset unpacked folder
			DirectoryInfo unpackedDir = new DirectoryInfo(Path.Combine(modDir.FullName, "unpacked"));
			if (unpackedDir.Exists)
			{
				FileHelper.ForceDelete(unpackedDir);
				unpackedDir.Refresh();
			}
			unpackedDir.Create();

			// unzip each download
			foreach (FileInfo archiveFile in packedDir.GetFiles())
			{
				ConsoleHelper.AutoRetry(() =>
				{
					progress.Caption = $"Unpacking {siteDir.Name} > {modDir.Name} > {archiveFile.Name} ({progress.Percent}%)...";

				// validate
				if (archiveFile.Extension == ".exe")
					{
						ConsoleHelper.Print($"  Skipped {archiveFile.FullName} (not an archive).", Severity.Error);
						return;
					}

				// unzip into temporary folder
				string id = Path.GetFileNameWithoutExtension(archiveFile.Name);
					DirectoryInfo tempDir = new DirectoryInfo(Path.Combine(unpackedDir.FullName, "_tmp", $"{archiveFile.Name}"));
					if (tempDir.Exists)
						FileHelper.ForceDelete(tempDir);
					tempDir.Create();
					tempDir.Refresh();

					try
					{
						this.ExtractFile(archiveFile, tempDir);
					}
					catch (Exception ex)
					{
						ConsoleHelper.Print($"  Could not unpack {archiveFile.FullName}:\n{(ex is SevenZipArchiveException ? ex.Message : ex.ToString())}", Severity.Error);
						Console.WriteLine();
						FileHelper.ForceDelete(tempDir);
						return;
					}

				// move into final location
				if (tempDir.EnumerateFiles().Any() || tempDir.EnumerateDirectories().Count() > 1) // no root folder in zip
					tempDir.Parent.MoveTo(Path.Combine(unpackedDir.FullName, id));
					else
					{
						tempDir.MoveTo(Path.Combine(unpackedDir.FullName, id));
						FileHelper.ForceDelete(new DirectoryInfo(Path.Combine(unpackedDir.FullName, "_tmp")));
					}
				});
			}
		}
		progress.Caption = $"Unpacked {progress.Total} mods from {siteDir.Name} (100%)";
	}
}

/// <summary>Parse unpacked mod data in the given folder.</summary>
/// <param name="rootPath">The full path to the folder containing unpacked mod files.</param>
IEnumerable<ParsedMod> ReadMods(string rootPath)
{
	ModToolkit toolkit = new ModToolkit();

	foreach (DirectoryInfo siteFolder in this.GetSortedSubfolders(new DirectoryInfo(rootPath)))
	{
		var modFolders = this.GetSortedSubfolders(siteFolder).ToArray();
		var progress = new IncrementalProgressBar(modFolders.Length).Dump();

		foreach (DirectoryInfo modFolder in modFolders)
		{
			progress.Increment();
			progress.Caption = $"Reading {siteFolder.Name} > {modFolder.Name}...";

			// read metadata files
			GenericMod metadata = JsonConvert.DeserializeObject<GenericMod>(File.ReadAllText(Path.Combine(modFolder.FullName, "mod.json")));
			IDictionary<int, GenericFile> fileMap = metadata.Files.ToDictionary(p => p.ID);

			// load mod folders
			IDictionary<GenericFile, ModFolder[]> unpackedFileFolders = new Dictionary<GenericFile, ModFolder[]>();
			DirectoryInfo unpackedFolder = new DirectoryInfo(Path.Combine(modFolder.FullName, "unpacked"));
			if (unpackedFolder.Exists)
			{
				foreach (DirectoryInfo fileDir in this.GetSortedSubfolders(unpackedFolder))
				{
					progress.Caption = $"Reading {siteFolder.Name} > {modFolder.Name} > {fileDir.Name}...";

					// get Nexus file data
					GenericFile fileData = fileMap[int.Parse(fileDir.Name)];

					// get mod folders from toolkit
					ModFolder[] mods = toolkit.GetModFolders(rootPath: unpackedFolder.FullName, modPath: fileDir.FullName).ToArray();
					if (mods.Length == 0)
					{
						ConsoleHelper.Print($"   Ignored {fileDir.FullName}, folder is empty?");
						continue;
					}

					// store metadata
					unpackedFileFolders[fileData] = mods;
				}
			}

			// yield mod
			yield return new ParsedMod(metadata, unpackedFileFolders);
		}
		progress.Caption = $"Read {progress.Total} mods from {siteFolder.Name} (100%)";
	}
}

/// <summary>Get the subfolders of a given folder sorted by numerical or alphabetical order.</summary>
/// <param name="root">The folder whose subfolders to get.</param>
private IEnumerable<DirectoryInfo> GetSortedSubfolders(DirectoryInfo root)
{
	return
		(
			from subfolder in root.GetDirectories()
			let isNumeric = int.TryParse(subfolder.Name, out int _)
			let numericName = isNumeric ? int.Parse(subfolder.Name) : int.MaxValue
			orderby numericName, subfolder.Name
			select subfolder
		);
}

/// <summary>Extract an archive file to the given folder.</summary>
/// <param name="file">The archive file to extract.</param>
/// <param name="extractTo">The directory to extract into.</param>
void ExtractFile(FileInfo file, DirectoryInfo extractTo)
{
	try
	{
		CancellationTokenSource cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
		Task
			.Run(() =>
			{
				using (SevenZipExtractor unpacker = new SevenZipExtractor(file.FullName))
					unpacker.ExtractArchive(extractTo.FullName);
			}, cancellation.Token)
			.Wait();
	}
	catch (AggregateException outerEx)
	{
		throw outerEx.InnerException;
	}
}

/// <summary>Get whether a given mod and file ID should be ignored when validating mods.</summary>
/// <param name="modID">The Nexus mod ID.</param>
/// <param name="fileID">The Nexus file ID.</param>
private bool ShouldIgnoreForValidation(int modID, int fileID)
{
	return this.IgnoreNexusIDsForValidation.Contains(modID) || this.IgnoreFileIDsForValidation.Contains(fileID);
}

/// <summary>Get a human-readable formatted time span.</summary>
/// <param name="span">The time span to format.</param>
private string GetFormattedTime(TimeSpan span)
{
	int hours = (int)span.TotalHours;
	int minutes = (int)span.TotalMinutes - (hours * 60);
	return $"{hours:00}:{minutes:00}";
}

/// <summary>Metadata for a mod from any mod site.</summary>
class GenericMod
{
	/*********
	** Accessors
	*********/
	/// <summary>The mod site which has the mod.</summary>
	public string Site { get; set; }

	/// <summary>The mod ID within the site.</summary>
	public int ID { get; set; }

	/// <summary>The mod display name.</summary>
	public string Name { get; set; }

	/// <summary>The mod author name.</summary>
	public string Author { get; set; }

	/// <summary>Custom author text, if different from <see cref="Author" />.</summary>
	public string AuthorLabel { get; set; }

	/// <summary>The URL to the user-facing mod page.</summary>
	public string PageUrl { get; set; }

	/// <summary>The main mod version, if applicable.</summary>
	public string Version { get; set; }

	/// <summary>When the mod metadata or files were last updated.</summary>
	public DateTimeOffset Updated { get; set; }

	/// <summary>The original data from the mod site.</summary>
	public object RawData { get; set; }

	/// <summary>The available mod downloads.</summary>
	public GenericFile[] Files { get; set; }


	/*********
	** Public methods
	*********/
	/// <summary>Construct an instance.</summary>
	public GenericMod() { }

	/// <summary>Construct an instance.</summary>
	/// <param name="site">The mod site which has the mod.</param>
	/// <param name="id">The mod ID within the site.</param>
	/// <param name="name">The mod display name.</param>
	/// <param name="author">The mod author name.</param>
	/// <param name="authorLabel">Custom author text, if different from <paramref name="author" />.</param>
	/// <param name="pageUrl">The URL to the user-facing mod page.</param>
	/// <param name="version">The main mod version, if applicable.</param>
	/// <param name="updated">When the mod metadata or files were last updated.</param>
	/// <param name="rawData">The original data from the mod site.</param>
	/// <param name="files">The available mod downloads.</param>
	public GenericMod(string site, int id, string name, string author, string authorLabel, string pageUrl, string version, DateTimeOffset updated, object rawData, GenericFile[] files)
	{
		this.Site = site;
		this.ID = id;
		this.Name = name;
		this.Author = author;
		this.AuthorLabel = authorLabel;
		this.PageUrl = pageUrl;
		this.Version = version;
		this.Updated = updated;
		this.RawData = rawData;
		this.Files = files;
	}
}

/// <summary>Parsed data about a mod page.</summary>
class ParsedMod : GenericMod
{
	/*********
	** Accessors
	*********/
	/// <summary>The parsed mod folders.</summary>
	public ParsedFile[] ModFolders { get; }


	/*********
	** Public methods
	*********/
	/// <summary>Construct an instance.</summary>
	/// <param name="mod">The raw mod metadata.</param>
	/// <param name="downloads">The raw mod download data.</param>
	public ParsedMod(GenericMod mod, IDictionary<GenericFile, ModFolder[]> downloads)
		: base(site: mod.Site, id: mod.ID, name: mod.Name, author: mod.Author, authorLabel: mod.AuthorLabel, pageUrl: mod.PageUrl, version: mod.Version, updated: mod.Updated, rawData: mod.RawData, files: mod.Files)
	{
		try
		{
			// set mod folders
			this.ModFolders =
				(
					from entry in downloads
					from folder in entry.Value
					select new ParsedFile(entry.Key, folder)
				)
				.ToArray();
		}
		catch (Exception)
		{
			new { mod, downloads }.Dump("failed parsing mod data");
			throw;
		}
	}
}

/// <summary>A file category on a mod site.</summary>
public enum GenericFileType
{
	/// <summary>The primary download.</summary>
	Main,

	/// <summary>A secondary download, often for preview or beta versions.</summary>
	Optional
}

/// <summary>Metadata for a mod download on any mod site.</summary>
public class GenericFile
{
	/*********
	** Accessors
	*********/
	/// <summary>The file ID.</summary>
	public int ID { get; set; }

	/// <summary>The file type.</summary.
	public GenericFileType Type { get; set; }

	/// <summary>The display name on the mod site.</summary>
	public string DisplayName { get; set; }

	/// <summary>The file name on the mod site.</summary>
	public string FileName { get; set; }

	/// <summary>The file version on the mod site.</summary>
	public string Version { get; set; }

	/// <summary>The original file data from the mod site.</summary>
	public object RawData { get; set; }


	/*********
	** Public methods
	*********/
	/// <summary>Construct an instance.</summary>
	public GenericFile() { }

	/// <summary>Construct an instance.</summary>
	/// <param name="id">The file ID.</param>
	/// <param name="type">The file type.</param>
	/// <param name="displayName">The display name on the mod site.</param>
	/// <param name="fileName">The filename on the mod site.</param>
	/// <param name="version">The file version on the mod site.</param>
	/// <param name="rawData">The original file data from the mod site.</param>
	public GenericFile(int id, GenericFileType type, string displayName, string fileName, string version, object rawData)
	{
		this.ID = id;
		this.Type = type;
		this.DisplayName = displayName;
		this.FileName = fileName;
		this.Version = version;
		this.RawData = rawData;
	}
}

/// <summary>Parsed data about a mod download.</summary>
class ParsedFile : GenericFile
{
	/*********
	** Accessors
	*********/
	/// <summary>The mod display name based on the manifest.</summary>
	public string ModDisplayName { get; }

	/// <summary>The mod type.</summary>
	public ModType ModType { get; }

	/// <summary>The mod parse error, if it could not be parsed.</summary>
	public ModParseError? ModError { get; }

	/// <summary>The mod ID from the manifest.</summary>
	public string ModID { get; }

	/// <summary>The mod version from the manifest.</summary>
	public string ModVersion { get; }

	/// <summary>The raw parsed mod folder.</summary>
	public Lazy<ModFolder> RawFolder { get; }


	/*********
	** Public methods
	*********/
	/// <summary>Construct an instance.</summary>
	/// <param name="download">The raw mod file.</param>
	/// <param name="folder">The raw parsed mod folder.</param>
	public ParsedFile(GenericFile download, ModFolder folder)
		: base(id: download.ID, type: download.Type, displayName: download.DisplayName, fileName: download.FileName, version: download.Version, rawData: download.RawData)
	{
		this.RawFolder = new Lazy<ModFolder>(() => folder);

		this.ModDisplayName = folder.DisplayName;
		this.ModType = folder.Type;
		this.ModError = folder.ManifestParseError == ModParseError.None ? (ModParseError?)null : folder.ManifestParseError;
		this.ModID = folder.Manifest?.UniqueID;
		this.ModVersion = folder.Manifest?.Version?.ToString();
	}
}

/// <summary>Handles the logic for deciding which mods to fetch.</summary>
interface ISelectStrategy
{
	/// <summary>Get the mod IDs to try fetching.</summary>
	/// <param name="nexus">The mod site API client.</param>
	Task<int[]> GetModIds(IModSiteClient modSite);
}

/// <summary>Fetch all mods starting from a given mod ID.</summary>
class FetchAllFromStrategy : ISelectStrategy
{
	/*********
	** Fields
	*********/
	/// <summary>The minimum mod ID to fetch.</summary>
	private int StartFrom;


	/*********
	** Public methods
	*********/
	/// <summary>Construct an instance.</summary>
	/// <param name="startFrom">The minimum mod ID to fetch.</param>
	public FetchAllFromStrategy(int startFrom)
	{
		this.StartFrom = startFrom;
	}

	/// <summary>Get the mod IDs to try fetching.</summary>
	/// <param name="modSite">The mod site API client.</param>
	public virtual async Task<int[]> GetModIds(IModSiteClient modSite)
	{
		int minID = Math.Max(this.StartFrom, await modSite.GetMinIdAsync());
		int maxID = await modSite.GetLatestIdAsync();

		if (minID > maxID)
			return new int[0];

		return Enumerable.Range(minID, maxID - minID + 1).ToArray();
	}
}

/// <summary>Fetch mods which were updated since the given date.</summary>
class FetchUpdatedStrategy : FetchAllFromStrategy
{
	/*********
	** Fields
	*********/
	/// <summary>The date from which to fetch mod data, or <c>null</c> for no date filter. Mods last updated before this date will be ignored.</summary>
	private DateTimeOffset StartFrom;


	/*********
	** Public methods
	*********/
	/// <summary>Construct an instance.</summary>
	/// <param name="startFrom">The minimum date from which to start fetching.</param>
	public FetchUpdatedStrategy(DateTimeOffset startFrom)
		: base(startFrom: 1)
	{
		this.StartFrom = startFrom;
	}

	/// <summary>Construct an instance.</summary>
	/// <param name="startFrom">The amount of time to fetch, working back from today.</param>
	public FetchUpdatedStrategy(TimeSpan startFrom)
		: this(DateTimeOffset.UtcNow.Subtract(startFrom)) { }

	/// <summary>Get the mod IDs to try fetching.</summary>
	/// <param name="modSite">The mod site API client.</param>
	public override async Task<int[]> GetModIds(IModSiteClient modSite)
	{
		return await modSite.GetModsUpdatedSinceAsync(this.StartFrom);
	}
}

/// <summary>An exception raised when API client exceeds the rate limits for an API.</summary>
class RateLimitedException : Exception
{
	/*********
	** Accessors
	*********/
	/// <summary>The amount of time to wait until it's safe to retry the request.</summary>
	public TimeSpan TimeUntilRetry { get; }

	/// <summary>A human-readable of current rate limit values, if available.</summary>
	public string RateLimitSummary { get; }


	/*********
	** Accessors
	*********/
	public RateLimitedException(TimeSpan timeUntilRetry, string rateLimitSummary)
		: base("Rate limits have been exceeded for this API.")
	{
		this.TimeUntilRetry = timeUntilRetry;
		this.RateLimitSummary = rateLimitSummary;
	}
}

/// <summary>The identifier for a mod site used in update keys.</summary>
enum ModSite
{
	/// <summary>The Nexus Mods site.</summary>
	Nexus
}

/// <summary>A client which fetches mod from a particular mod site.</summary>
interface IModSiteClient
{
	/*********
	** Accessors
	*********/
	/// <summary>The identifier for this mod site used in update keys.</summary>
	ModSite SiteKey { get; }
	

	/*********
	** Methods
	*********/
	/// <summary>Get the current lowest mod ID.</summary>
	/// <exception cref="RateLimitedException">The API client has exceeded the API's rate limits.</exception>
	Task<int> GetMinIdAsync();

	/// <summary>Get the current highest mod ID.</summary>
	/// <exception cref="RateLimitedException">The API client has exceeded the API's rate limits.</exception>
	Task<int> GetLatestIdAsync();

	/// <summary>Get a mod from the mod site API.</summary>
	/// <param name="id">The mod ID to fetch.</param>
	/// <exception cref="KeyNotFoundException">The mod site has no mod with that ID.</exception>
	/// <exception cref="RateLimitedException">The API client has exceeded the API's rate limits.</exception>
	Task<GenericMod> GetModAsync(int id);

	/// <summary>Get the download URLs for a given file. If this returns multiple URLs, they're assumed to be mirrors and the first working URL will be used.</summary>
	/// <param name="mod">The mod for which to get a download URL.</param>
	/// <param name="file">The file for which to get a download URL.</param>
	/// <exception cref="RateLimitedException">The API client has exceeded the API's rate limits.</exception>
	Task<Uri[]> GetDownloadUrlsAsync(GenericMod mod, GenericFile file);

	/// <summary>Get all mod IDs updated since the given date.</summary>
	/// <param name="startFrom">The minimum date from which to start fetching.</param>
	/// <exception cref="RateLimitedException">The API client has exceeded the API's rate limits.</exception>
	Task<int[]> GetModsUpdatedSinceAsync(DateTimeOffset startFrom);
}

/// <summary>A client which fetches mod from the Nexus Mods API.</summary>
class NexusApiClient : IModSiteClient
{
	/*********
	** Fields
	*********/
	/// <summary>The Nexus Mods game key for Stardew Valley.</summary>
	private readonly string GameKey = "stardewvalley";
	
	/// <summary>The underlying FluentNexus API client.</summary>
	private NexusClient Nexus;


	/*********
	** Accessors
	*********/
	/// <summary>The identifier for this mod site used in update keys.</summary>
	public ModSite SiteKey { get; } = ModSite.Nexus;


	/*********
	** Public methods
	*********/
	/// <summary>Construct an instance.</summary>
	/// <param name="apiKey">The Nexus API key with which to authenticate.</param>
	/// <param name="appName">An arbitrary name for the app/script using the client, reported to the Nexus Mods API and used in the user agent.</param>
	/// <param name="appVersion">An arbitrary version number for the <paramref name="appName" /> (ideally a semantic version).</param>
	public NexusApiClient(string apiKey, string appName, string appVersion)
	{
		this.Nexus = new NexusClient(apiKey, appName, appVersion);
	}

	/// <summary>Get the current lowest mod ID.</summary>
	/// <exception cref="RateLimitedException">The API client has exceeded the API's rate limits.</exception>
	public Task<int> GetMinIdAsync()
	{
		return Task.FromResult(1);
	}

	/// <summary>Get the current highest mod ID.</summary>
	/// <exception cref="RateLimitedException">The API client has exceeded the API's rate limits.</exception>
	public async Task<int> GetLatestIdAsync()
	{
		try
		{
			return
				(await this.Nexus.Mods.GetLatestAdded(this.GameKey))
				.Max(p => p.ModID);
		}
		catch (ApiException ex) when (ex.Status == (HttpStatusCode)429)
		{
			throw await this.GetRateLimitExceptionAsync();
		}
	}

	/// <summary>Get a mod from the mod site API.</summary>
	/// <param name="id">The mod ID to fetch.</param>
	/// <exception cref="KeyNotFoundException">The mod site has no mod with that ID.</exception>
	/// <exception cref="RateLimitedException">The API client has exceeded the API's rate limits.</exception>
	public async Task<GenericMod> GetModAsync(int id)
	{
		try
		{
			// fetch mod data
			Mod nexusMod;
			try
			{
				nexusMod = await this.Nexus.Mods.GetMod(this.GameKey, id);
			}
			catch (ApiException ex) when (ex.Status == HttpStatusCode.NotFound)
			{
				throw new KeyNotFoundException($"There is no Nexus mod with ID {id}");
			}

			// fetch file data
			ModFile[] nexusFiles = nexusMod.Status == ModStatus.Published
				? (await this.Nexus.ModFiles.GetModFiles(this.GameKey, id, FileCategory.Main, FileCategory.Optional)).Files
				: new ModFile[0];

			// create file models
			GenericFile[] files = nexusFiles
				.Select(file =>
				{
					GenericFileType type = file.Category switch
					{
						FileCategory.Main => GenericFileType.Main,
						FileCategory.Optional => GenericFileType.Optional,
						_ => throw new InvalidOperationException($"Unknown file category from Nexus: {file.Category}")
					};
					return new GenericFile(id: file.FileID, type: type, displayName: file.Name, fileName: file.FileName, version: file.FileVersion, rawData: file);
				})
				.ToArray();

			// create mod model
			GenericMod mod;
			{
				string author = nexusMod.User?.Name ?? nexusMod.Author;
				string authorLabel = nexusMod.Author != null && !nexusMod.Author.Equals(author, StringComparison.InvariantCultureIgnoreCase)
					? nexusMod.Author
					: null;

				mod = new GenericMod(
					site: "Nexus",
					id: nexusMod.ModID,
					name: nexusMod.Name,
					author: author,
					authorLabel: authorLabel,
					pageUrl: $"https://www.nexusmods.com/stardewvalley/mods/{nexusMod.ModID}",
					version: nexusMod.Version,
					updated: nexusMod.Updated,
					rawData: nexusMod,
					files: files
				);
			}

			return mod;
		}
		catch (ApiException ex) when (ex.Status == (HttpStatusCode)429)
		{
			throw await this.GetRateLimitExceptionAsync();
		}
	}

	/// <summary>Get the download URLs for a given file. If this returns multiple URLs, they're assumed to be mirrors and the first working URL will be used.</summary>
	/// <param name="mod">The mod for which to get a download URL.</param>
	/// <param name="file">The file for which to get a download URL.</param>
	public async Task<Uri[]> GetDownloadUrlsAsync(GenericMod mod, GenericFile file)
	{
		ModFileDownloadLink[] downloadLinks = await this.Nexus.ModFiles.GetDownloadLinks(this.GameKey, mod.ID, file.ID);
		return downloadLinks.Select(p => p.Uri).ToArray();
	}

	/// <summary>Get all mod IDs updated since the given date.</summary>
	/// <param name="startFrom">The minimum date from which to start fetching.</param>
	public async Task<int[]> GetModsUpdatedSinceAsync(DateTimeOffset startFrom)
	{
		// calculate update period
		string updatePeriod = null;
		{
			TimeSpan duration = DateTimeOffset.UtcNow - startFrom;
			if (duration.TotalDays <= 1)
				updatePeriod = "1d";
			else if (duration.TotalDays <= 7)
				updatePeriod = "1w";
			else if (duration.TotalDays <= 28)
				updatePeriod = "1m";
			else
				throw new NotSupportedException($"The given date ({startFrom}) can't be used with {this.GetType().Name} because it exceeds the maximum update period of 28 days for the Nexus API.");
		}

		List<int> modIDs = new List<int>();
		foreach (ModUpdate mod in await this.Nexus.Mods.GetUpdated(this.GameKey, updatePeriod))
		{
			if (mod.LatestFileUpdate >= startFrom)
				modIDs.Add(mod.ModID);
		}
		return modIDs.ToArray();
	}


	/*********
	** Private methods
	*********/
	/// <summary>Get an exception indicating that rate limits have been exceeded.</summary>
	private async Task<RateLimitedException> GetRateLimitExceptionAsync()
	{
		IRateLimitManager rateLimits = await this.Nexus.GetRateLimits();
		TimeSpan unblockTime = rateLimits.GetTimeUntilRenewal();
		throw new RateLimitedException(unblockTime, this.GetRateLimitSummary(rateLimits));
	}
	
	/// <summary>Get a human-readable summary for the current rate limits.</summary>
	/// <param name="meta">The current rate limits.</param>
	private string GetRateLimitSummary(IRateLimitManager meta)
	{
		return $"{meta.DailyRemaining}/{meta.DailyLimit} daily resetting in {this.GetFormattedTime(meta.DailyReset - DateTimeOffset.UtcNow)}, {meta.HourlyRemaining}/{meta.HourlyLimit} hourly resetting in {this.GetFormattedTime(meta.HourlyReset - DateTimeOffset.UtcNow)}";
	}

	/// <summary>Get a human-readable formatted time span.</summary>
	/// <param name="span">The time span to format.</param>
	private string GetFormattedTime(TimeSpan span)
	{
		int hours = (int)span.TotalHours;
		int minutes = (int)span.TotalMinutes - (hours * 60);
		return $"{hours:00}:{minutes:00}";
	}
}