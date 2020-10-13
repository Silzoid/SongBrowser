using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using MelonLoader;
using UnityEngine;
using MelonLoader.TinyJSON;
using System.IO;
using Harmony;
using System.Media;
using OggDecoder;
using System.Linq;
using TMPro;
using UnityEngine.Events;
[assembly: MelonOptionalDependencies("SongDataLoader")]

namespace AudicaModding
{
    public class SongBrowser : MelonMod
    {
        public static class BuildInfo
        {
            public const string Name = "SongBrowser";  // Name of the Mod.  (MUST BE SET)
            public const string Author = "octo"; // Author of the Mod.  (Set as null if none)
            public const string Company = null; // Company that made the Mod.  (Set as null if none)
            public const string Version = "2.1.0"; // Version of the Mod.  (MUST BE SET)
            public const string DownloadLink = null; // Download Link for the Mod.  (Set as null if none)
        }
        public static string apiURL = "http://www.audica.wiki:5000/api/customsongs?pagesize=14";
        public string downloadPath = null;
        public static APISongList songlist;
        public APISongList fullSongList;
        public static Vector3 DebugTextPosition = new Vector3(0f, -1f, 5f);
        public static bool shouldShowKeyboard = false;
        public static string searchString = "";
        public static bool needRefresh = false;
        public static int page = 1;
        public static string customSongDirectory;
        public static string downloadsDirectory;
        public static bool emptiedDownloadsFolder = false;
        public static bool addedCustomsDir = false;
        public static List<string> deletedSongs = new List<string>();
        public static List<string> deletedSongPaths = new List<string>();
        public static int newSongCount;
        public static int lastSongCount;

        //Meeps' Stuff
        public static bool songDataLoaderInstalled = false;
        public class SongDisplayPackage
        {
            public bool hasEasy = false;
            public bool hasStandard = false;
            public bool hasAdvanced = false;
            public bool hasExpert = false;

            //360 tag
            public bool easy360 = false;
            public bool standard360 = false;
            public bool advanced360 = false;
            public bool expert360 = false;

            public static SongDisplayPackage Fill360Data(SongDisplayPackage songd, string songID)
            {
                if (SongDataLoader.AllSongData.ContainsKey(songID))
                {
                    SongDataLoader.SongData currentSong = SongDataLoader.AllSongData[songID];
                    if (currentSong.HasCustomData())
                    {
                        if (currentSong.SongHasCustomDataKey("easy360"))
                        {
                            songd.easy360 = currentSong.GetCustomData<bool>("easy360");
                        }

                        if (currentSong.SongHasCustomDataKey("standard360"))
                        {
                            songd.standard360 = currentSong.GetCustomData<bool>("standard360");
                        }

                        if (currentSong.SongHasCustomDataKey("advanced360"))
                        {
                            songd.advanced360 = currentSong.GetCustomData<bool>("advanced360");
                        }

                        if (currentSong.SongHasCustomDataKey("expert360"))
                        {
                            songd.expert360 = currentSong.GetCustomData<bool>("expert360");
                        }
                    }
                }
                return songd;
            }
        }

        private void CreateConfig()
        {
            MelonPrefs.RegisterInt("RandomSong", "RandomSongBagSize", 10);
            MelonPrefs.RegisterInt("SongBrowser", "LastSongCount", 0);
        }

        private void LoadConfig()
        {
            RandomSong.LoadBagSize(MelonPrefs.GetInt("RandomSong", "RandomSongBagSize"));
            lastSongCount = MelonPrefs.GetInt("SongBrowser", "LastSongCount");
        }

        public static void SaveConfig()
        {
            MelonPrefs.SetInt("RandomSong", "RandomSongBagSize", RandomSong.randomSongBagSize);
            MelonPrefs.SetInt("SongBrowser", "LastSongCount", lastSongCount);
        }

        public override void OnLevelWasLoaded(int level)
        {

            if (!MelonPrefs.HasKey("RandomSong", "RandomSongBagSize") || !MelonPrefs.HasKey("SongBrowser", "LastSongCount"))
            {
                CreateConfig();
            }
            else
            {
                LoadConfig();
            }
        }

        public override void OnApplicationStart()
        {
            downloadsDirectory = Application.dataPath.Replace("Audica_Data", "Downloads");
            customSongDirectory = Application.dataPath.Replace("Audica_Data", "CustomSongs");
            CheckFolderDirectories();
            StartSongSearch();
            var i = HarmonyInstance.Create("Song Downloader");
            FilterPanel.LoadFavorites();

            if (MelonHandler.Mods.Any(it => it.Info.SystemType.Name == nameof(SongDataLoader)))
            {
                songDataLoaderInstalled = true;
                MelonLogger.Log("Song Data Loader is installed. Enabling integration");
            }
            else
                MelonLogger.LogWarning("Song Data Loader is not installed. Consider downloading it for the best experience :3");
        }

        private void CheckFolderDirectories()
        {
            if (!Directory.Exists(downloadsDirectory))
            {
                Directory.CreateDirectory(downloadsDirectory);
            }
            if (!Directory.Exists(customSongDirectory))
            {
                Directory.CreateDirectory(customSongDirectory);
            }
        }

        //public override void OnGUI()
        //{
        //    if (GUI.Button(new Rect(10, 10, 150, 100), "Show scores"))
        //    {
        //        ScoreDisplayList.Initialize();
        //    }
        //    if (GUI.Button(new Rect(10, 110, 150, 100), "Update"))
        //    {
        //        ScoreDisplayList.UpdateTextFromList();
        //    }
        //}

        public override void OnApplicationQuit()
        {
            FilterPanel.SaveFavorites();
            CleanDeletedSongs();
        }

        public static void RestoreDeletedSongs()
        {
            deletedSongPaths = new List<string>();
            deletedSongs = new List<string>();
            DebugText("Restored songs");
        }

        public static void CleanDeletedSongs()
        {
            foreach (var songPath in deletedSongPaths)
            {
                if (File.Exists(songPath))
                {
                    File.Delete(songPath);
                }
            }
        }

        public static void RemoveSong(string songID)
        {
            if (deletedSongs.Contains(songID))
            {
                return;
            }
            var song = SongList.I.GetSong(songID);
            deletedSongPaths.Add(song.searchRoot + "/" + song.zipPath);
            deletedSongs.Add(song.songID);
        }

        public override void OnUpdate()
        {
            if (Input.GetKeyDown(KeyCode.F5))
            {
                ReloadSongList();
            }
            //if (Input.GetKeyDown(KeyCode.F3))
            //{
            //    FilterPanel.GetReferences();
            //    FilterPanel.SetNotificationText("There are 3 new songs available in the song downloader.");
            //}
        }

        IEnumerator PlayOggCoroutine(string oggFilename)
        {
            using (var file = new FileStream(oggFilename, FileMode.Open, FileAccess.Read))
            {
                var player = new SoundPlayer(new OggDecodeStream(file));
                player.Play();
                yield return new WaitForSeconds(10f);
            }
            yield return null;
        }


        public static void ReloadSongList()
        {
            needRefresh = false;
            SongList.sFirstTime = true;
            SongList.OnSongListLoaded.mDone = false;
            SongList.SongSourceDirs = new Il2CppSystem.Collections.Generic.List<SongList.SongSourceDir>();
            SongList.AddSongSearchDir(Application.dataPath, downloadsDirectory);
            SongList.I.StartAssembleSongList();
            SongSelect songSelect = GameObject.FindObjectOfType<SongSelect>();
            if (songSelect != null)
            {
                SongList.OnSongListLoaded.On(new Action(() => { songSelect.ShowSongList(); }));
            }

            if (songDataLoaderInstalled)
            {
                SongDataLoader.ReloadSongData();
                MelonLogger.Log("Song Data Reloaded");
            }

            DebugText("Reloading Songs");
			
        }

        public static void StartSongSearch()
        {
            MelonCoroutines.Start(StartSongSearchCoroutine(searchString, SongDownloaderUI.difficultyFilter.ToString(), page, false));
        }

        public static IEnumerator StartSongSearchCoroutine(string search, string difficulty = null, int page = 1, bool total = false)
        {
            string webSearch = search == null || search == "" ? "" : "&search=" + WebUtility.UrlEncode(search);
            string webPage = page == 1 ? "" : "&page=" + page.ToString();
            string webDifficulty = difficulty == "All" || difficulty == "" ? "" : "&" + difficulty.ToLower() + "=true";
            string webCurated = SongDownloaderUI.curated ? "&curated=true" : "";
            string webPlaycount = SongDownloaderUI.popularity ? "&sort=leaderboards" : "";
            string concatURL = !total ? apiURL + webSearch + webDifficulty + webPage + webCurated + webPlaycount : "http://www.audica.wiki:5000/api/customsongs?pagesize=all";
            WWW www = new WWW(concatURL);
            yield return www;
            songlist = JSON.Load(www.text).Make<APISongList>();
            if (SongDownloaderUI.songItemPanel != null)
            {
                SongDownloaderUI.AddSongItems(SongDownloaderUI.songItemMenu, songlist);
            }
        }


        public static IEnumerator DownloadSong(string downloadUrl)
        {
            string[] splitURL = downloadUrl.Split('/');
            string audicaName = splitURL[splitURL.Length - 1];
            string path = Application.streamingAssetsPath + "\\HmxAudioAssets\\songs\\" + audicaName;
            string customSongsPath = customSongDirectory + "\\" + audicaName;
            string downloadPath = downloadsDirectory + "\\" + audicaName;
            if (!File.Exists(path) && !File.Exists(downloadPath) && !File.Exists(downloadPath))
            {
                WWW www = new WWW(downloadUrl);
                yield return www;
                byte[] results = www.bytes;
                File.WriteAllBytes(downloadPath, results);
                needRefresh = true;
            }
            yield return null;
        }


        public static IEnumerator StreamPreviewSong(string url)
        {
            WWW www = new WWW(url);
            yield return www;
            if (www.isDone)
            {
                Stream stream = new MemoryStream(www.bytes);
                var player = new SoundPlayer(new OggDecodeStream(stream));
                //player.LoadAsync();
                yield return new WaitForSeconds(0.2f);
                player.Play();
                yield return new WaitForSeconds(15f);
            }

            yield return null;
        }

        public static IEnumerator UpdateLastSongCount()
        {
            string URL = "http://www.audica.wiki:5000/api/customsongstotal";
            WWW www = new WWW(URL);
            yield return www;
            var songcount = JSON.Load(www.text).Make<TotalSongs>();
            newSongCount = songcount.song_count;
            if (FilterPanel.notificationPanel != null)
            {
                if (lastSongCount == newSongCount) FilterPanel.SetNotificationText("There are no new songs available");
                else
                {
                    int _count = newSongCount - lastSongCount;
                    bool isSingular = (newSongCount - lastSongCount) == 1;
                    string preSongtxt = isSingular ? "is " : "are ";
                    string songtxt = isSingular ? "song" : "songs";
                    FilterPanel.SetNotificationText("There " + preSongtxt + _count.ToString() + " new " + songtxt + " available");
                }
                    
            }
        }


        public static void DebugText(string text)
        {
            KataConfig.I.CreateDebugText(text, DebugTextPosition, 5f, null, false, 0.2f);
        }

        public static void NextPage()
        {
            if (page > songlist.total_pages)
                page = songlist.total_pages;
            else if (page < 1)
                page = 1;
            else
                page++;
        }
        public static void PreviousPage()
        {
            if (page == 1) return;
            if (page > songlist.total_pages)
                page = songlist.total_pages;
            else if (page < 1)
                page = 1;
            else
                page--;
        }


        public static string GetDifficultyString(SongDisplayPackage songD)
        {
            return "[" +
                (songD.hasEasy ? "<color=#54f719>B</color>" : "") + (songD.hasEasy && songD.easy360 ? "<color=#32a8a4>(360) </color>" : "") +
                (songD.hasStandard ? "<color=#19d2f7>S</color>" : "") + (songD.hasStandard && songD.standard360 ? "<color=#32a8a4> (360) </color>" : "") +
                (songD.hasAdvanced ? "<color=#f7a919>A</color>" : "") + (songD.advanced360 && songD.hasAdvanced ? "<color=#32a8a4> (360) </color>" : "") +
                (songD.hasExpert ? "<color=#b119f7>E</color>" : "") + (songD.hasExpert && songD.expert360 ? "<color=#32a8a4> (360)</color>" : "") +
                "]";
        }

        public static string RemoveFormatting(string input)
        {
            System.Text.RegularExpressions.Regex rx = new System.Text.RegularExpressions.Regex("<[^>]*>");
            return rx.Replace(input, "");
        }

    }
}



[Serializable]
public class APISongList
{
    public int total_pages;
    public int song_count;
    public Song[] songs;
    public int pagesize;
    public int page;
}

[Serializable]
public class Song
{
    public string song_id;
    public string author;
    public string title;
    public string artist;
    public bool beginner;
    public bool standard;
    public bool advanced;
    public bool expert;
    public string download_url;
    public string preview_url;
    public string upload_time;
    public int leaderboard_scores;
    public string video_url;
    public string filename;
    public DateTime GetDate()
    {
        string[] day = this.upload_time.Split(new char[] { ' ', '-' });
        string[] time = this.upload_time.Split(new char[] { ' ', ':', '.' });
        return new DateTime(Int32.Parse(day[0]),
            Int32.Parse(day[1]),
            Int32.Parse(day[2]),
            Int32.Parse(time[1]),
            Int32.Parse(time[2]),
            Int32.Parse(time[3]));
    }
}

[Serializable]
public class TotalSongs
{
    public int song_count;
    public int author_count;
}