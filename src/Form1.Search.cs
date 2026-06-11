using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Rocola
{
    public partial class Form1
    {
        private void ToggleSearch()
        {
            if (searchOpen)
            {
                CloseSearch();
            }
            else
            {
                OpenSearch();
            }
        }

        private void MarkSearchActivity()
        {
            searchLastActivity = DateTime.Now;
        }

        private void OpenSearch()
        {
            searchOpen = true;
            searchLastActivity = DateTime.Now;
            lastQuery = null;
            lastMatches = null;
            if (video != null)
            {
                video.Visible = false;
            }
            searchOverlay.Accent = bgAccent;
            searchOverlay.SetQuery("");
            searchOverlay.Results = new List<SearchItem>();
            searchOverlay.SelRow = 1;
            searchOverlay.SelCol = 0;
            searchOverlay.Bounds = new Rectangle(0, 0, ClientSize.Width, ClientSize.Height);
            searchOverlay.Visible = true;
            searchOverlay.BringToFront();
            searchOverlay.Focus();
            searchOverlay.Invalidate();
        }

        private void CloseSearch()
        {
            searchOpen = false;
            searchOverlay.Visible = false;
            if (videoOn && video != null)
            {
                video.Visible = true;
                video.BringToFront();
            }
            Focus();
        }

        // Las partes derivadas de la consulta (" " + q y sus palabras) se
        // calculan una sola vez por tecla en RecomputeResults, en lugar de
        // asignarlas por cada candidato del indice.
        private static int Score(string cand, string q, string spaceQ, string[] words)
        {
            if (q.Length == 0)
            {
                return 0;
            }
            if (cand == q)
            {
                return 10000;
            }
            if (cand.StartsWith(q, StringComparison.Ordinal))
            {
                return 6000 - Math.Min(cand.Length, 2000);
            }
            int wordStart = cand.IndexOf(spaceQ, StringComparison.Ordinal);
            if (wordStart >= 0)
            {
                return 4000 - wordStart;
            }
            int anywhere = cand.IndexOf(q, StringComparison.Ordinal);
            if (anywhere >= 0)
            {
                return 2000 - Math.Min(anywhere, 1500);
            }
            if (words != null)
            {
                int total = 0;
                int found = 0;
                foreach (string word in words)
                {
                    if (word.Length != 0)
                    {
                        found++;
                        int idx = cand.IndexOf(word, StringComparison.Ordinal);
                        if (idx < 0)
                        {
                            return 0;
                        }
                        bool atWordStart = idx == 0 || cand[idx - 1] == ' ';
                        total += (atWordStart ? 300 : 150) - Math.Min(idx, 100);
                    }
                }
                if (found >= 2)
                {
                    return 800 + total;
                }
            }
            return 0;
        }

        private void RecomputeResults(string query)
        {
            MarkSearchActivity();
            if (query != null && query.Trim() == "0000000000")
            {
                searchOverlay.SetQuery("");
                StartConfig();
                return;
            }
            string q = Normalize(query);
            List<SearchItem> results = new List<SearchItem>();
            if (q.Length > 0)
            {
                string spaceQ = " " + q;
                string[] words = (q.IndexOf(' ') >= 0) ? q.Split(new char[1] { ' ' }) : null;
                List<SearchItem> source = (lastQuery != null && lastQuery.Length > 0 && lastMatches != null && q.StartsWith(lastQuery, StringComparison.Ordinal)) ? lastMatches : searchIndex;
                List<SearchItem> matches = new List<SearchItem>();
                for (int i = 0; i < source.Count; i++)
                {
                    int score = Score(source[i].Key, q, spaceQ, words);
                    if (score > 0)
                    {
                        source[i].Score = score;
                        matches.Add(source[i]);
                    }
                }
                matches.Sort(delegate(SearchItem a, SearchItem b)
                {
                    if (b.Score != a.Score)
                    {
                        return b.Score - a.Score;
                    }
                    return (a.Kind != b.Kind) ? (a.Kind - b.Kind) : string.Compare(a.Display, b.Display, StringComparison.OrdinalIgnoreCase);
                });
                lastMatches = matches;
                lastQuery = q;
                int top = Math.Min(40, matches.Count);
                for (int i = 0; i < top; i++)
                {
                    results.Add(matches[i]);
                }
            }
            else
            {
                lastMatches = null;
                lastQuery = null;
            }
            searchOverlay.Results = results;
            searchOverlay.Invalidate();
        }

        private void ChooseSearchResult(SearchItem it)
        {
            if (it == null)
            {
                return;
            }
            MarkSearchActivity();
            if (it.Kind == 2 && it.TrackRef != null)
            {
                PlayItem item = new PlayItem();
                item.IsVideo = false;
                item.Song = it.TrackRef;
                item.Disc = it.AlbumRef;
                switch (AddItem(item))
                {
                    case AddResult.Added:
                        ShowSearchToast("Agregada a la cola", true);
                        break;
                    case AddResult.NoCredit:
                        ShowSearchToast("Inserta una moneda", false);
                        break;
                    default:
                        ShowSearchToast("Esa cancion ya va seguida", false);
                        break;
                }
            }
            else if (it.Kind == 1 && it.AlbumRef != null)
            {
                CloseSearch();
                NavigateToAlbum(it.AlbumRef);
                EnterAlbum(it.AlbumRef);
            }
            else if (it.Kind == 0 && it.AlbumRef != null)
            {
                CloseSearch();
                NavigateToAlbum(it.AlbumRef);
            }
        }

        private void ShowSearchToast(string msg)
        {
            ShowSearchToast(msg, true);
        }

        private void ShowSearchToast(string msg, bool ok)
        {
            searchOverlay.ToastMsg = msg;
            searchOverlay.ToastBack = ok ? Color.FromArgb(238, 40, 165, 90) : Color.FromArgb(238, 205, 130, 35);
            searchOverlay.ToastOn = true;
            searchOverlay.Invalidate();
            toastTimer.Stop();
            toastTimer.Start();
        }
    }
}
