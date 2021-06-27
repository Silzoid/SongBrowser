using Il2CppSystem.Collections.Generic;
using UnityEngine;

namespace AudicaModding
{
    internal static class RandomSong
    {
        private static int historySize = 10;

        private static List<string> currentSongs     = new List<string>();
        private static List<string> currentSongsFull = new List<string>();

        private static List<string> recentlySelected = new List<string>();

        public static void UpdateAvailableSongs(List<string> songs)
        {
            currentSongsFull = songs;

            FillSongList();

            if (currentSongsFull.Count == 0)
                RandomSongButton.Disable();
            else
                RandomSongButton.Enable();
        }

        public static void SelectRandomSong()
        {
            if (currentSongsFull.Count == 0)
                return;

            if (currentSongs.Count == 0)
            {
                recentlySelected.Clear();
                FillSongList();
            }

            int               songCount = currentSongs.Count;
            System.Random     rand      = new System.Random();
            int               idx       = rand.Next(0, songCount);
            string            songID    = currentSongs[idx];
            SongList.SongData data      = SongList.I.GetSong(songID);
            if (data != null)
            {
                // remove from the random songs list
                currentSongs.RemoveAt(idx);
                if (recentlySelected.Count > historySize)
                {
                    recentlySelected.RemoveAt(0);
                }
                recentlySelected.Add(songID);

                SongDataHolder.I.songData = data;
                MenuState.I.GoToLaunchPage();
            }
        }

        private static void FillSongList()
        {
            currentSongs = new List<string>();

            // remove recently selected songs from the list
            for (int i = 0; i < currentSongsFull.Count; i++)
            {
                string songID = currentSongsFull[i];
                if (!recentlySelected.Contains(songID))
                {
                    currentSongs.Add(songID);
                }
            }
        }
    }
}
