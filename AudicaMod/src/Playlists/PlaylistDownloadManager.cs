﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MelonLoader;
using TMPro;
using UnityEngine;

namespace AudicaModding
{
    internal class PlaylistDownloadManager
    {
        public Song[] songList { get; private set; }
        public static bool IsDownloadingMissing { get; set; } = false;
        public static int ActiveDownloads = 0;
        private bool missingSongsFound = false;
        private GunButton backButton = null;
        private TextMeshPro backButtonLabel = null;
        private bool playlistsPopulated = false;
        private bool prepareDownloadMissing = false;
        public void DownloadSingleSong(string filename, bool showPopup, GunButton button, TextMeshPro label)
        {
            if (backButton is null && button != null)
            {
                backButton = button;
                backButtonLabel = label;
                backButton.SetInteractable(false);
                backButtonLabel.text = "Loading..";
                backButtonLabel.alpha = .25f;
            }
            if (showPopup) PlaylistUtil.Popup("Downloading..");
            MelonLogger.Msg("Downloading " + filename);
            MelonCoroutines.Start(SongDownloader.DoSongWebSearch(filename, OnWebSearchDone, DifficultyFilter.All, false, 1, false, true));
        }

        public void DownloadSongs(List<string> filenames, bool showPopup, GunButton button, TextMeshPro label)
        {
            if (backButton is null && button != null)
            {
                backButton = button;
                backButtonLabel = label;
                backButton.SetInteractable(false);
                backButtonLabel.text = "Loading..";
                backButtonLabel.alpha = .25f;
            }
            if (showPopup) PlaylistUtil.Popup("Downloading..");
            foreach (string filename in filenames)
            {
                MelonCoroutines.Start(SongDownloader.DoSongWebSearch(filename, OnWebSearchDone, DifficultyFilter.All, false, 1, false, true));
            }
        }

        public void DownloadMissingSongs()
        {
            if (missingSongsFound) return;
            missingSongsFound = true;
            if (PlaylistManager.playlists is null || PlaylistManager.playlists.Values.Count == 0)
            {
                return;
            }
            IsDownloadingMissing = true;
            //prepareDownloadMissing = true;
            List<string> songs = new List<string>();
            foreach (Playlist playlist in PlaylistManager.playlists.Values)
            {
                if (PlaylistManager.IsPlaylistInitialized(playlist.name)) continue;
                PlaylistManager.SetPlaylistInitialized(playlist.name);
                foreach (string song in playlist.songs)
                {
                    if (!SongLoadingManager.songDictionary.ContainsKey(song + ".audica"))
                    {
                        //hasMissingSongs = true;
                        songs.Add(song + ".audica");
                        //DownloadSong(song + ".audica");
                    }
                    
                }
            }
            if (songs.Count > 0)
            {
                DownloadSongs(songs, false, null, null);
            }
            else
            {
                prepareDownloadMissing = false;
                IsDownloadingMissing = false;
                PopulatePlaylists();
                SongLoadingManager.UpdateUI();
            }
        }

        public void OnWebSearchDone(string search, APISongList result)
        {
            if (result is null)
            {
                /*if (IsDownloadingMissing && prepareDownloadMissing)
                {
                    IsDownloadingMissing = false;
                    prepareDownloadMissing = false;
                }*/
                MelonLogger.Msg("search returned no matches.");
                return;
            }
            if(result.song_count == 1)
            {
                Song song = result.songs[0];
                ActiveDownloads++;
                MelonCoroutines.Start(SongDownloader.DownloadSong(song.song_id, song.download_url, OnDownloadComplete));
            }
            else
            {
                MelonLogger.Msg("Multiple or no results found.");
                if(ActiveDownloads == 0) EnableBackButton();
            }
        }

        private void OnDownloadComplete(string search, bool success)
        {
            /*if(!IsDownloadingMissing && prepareDownloadMissing)
            {
                IsDownloadingMissing = true;
                prepareDownloadMissing = false;
            }*/
            
            ActiveDownloads -= 1;
            if (!success)
            {
                MelonLogger.Warning("Download of " + search + " failed");
                if (ActiveDownloads > 0) return;
                if (IsDownloadingMissing)
                {
                    IsDownloadingMissing = false;
                    SongLoadingManager.EnableButtons();
                    PlaylistUtil.Popup("Missing playlist songs downloaded.");
                    PopulatePlaylists();
                    SongBrowser.ReloadSongList();
                }
            }
            if (ActiveDownloads > 0) return;
            if (!IsDownloadingMissing)
            {
                SongBrowser.ReloadSongList();
                //EnableBackButton();
                return;
            }
            PlaylistManager.SavePlaylistData();
            //EnableBackButton();
            if (IsDownloadingMissing)
            {
                IsDownloadingMissing = false;
                SongLoadingManager.EnableButtons();
                PlaylistUtil.Popup("Missing playlist songs downloaded.");
                PopulatePlaylists();
                SongBrowser.ReloadSongList();
            }
        }

        public void EnableBackButton()
        {
            if(backButton != null)
            {
                backButton.SetInteractable(true);
                backButtonLabel.text = "Back";
                backButtonLabel.alpha = 1f;
                backButton = null;
                backButtonLabel = null;
            }
        }

        private void PopulatePlaylists()
        {
            if (!playlistsPopulated)
            {
                playlistsPopulated = true;
                PlaylistManager.PopulatePlaylistsSongNames();
            }
        }
    }
}
