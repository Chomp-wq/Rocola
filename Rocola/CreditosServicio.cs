using System;
using System.Collections.Generic;
namespace Rocola
{
    // Acceso local oculto, no autenticacion: quien conozca el texto puede utilizarlo.
    internal sealed class SecretCreditGate
    {
        private bool armed;
        private uint since;
        internal bool Ready { get; private set; }
        internal static bool Matches(string text) { return string.Equals(text, "CHOMPS21", StringComparison.OrdinalIgnoreCase); }
        internal void Update(bool open, string text, uint now)
        {
            if (!open || !Matches(text)) { armed = false; Ready = false; return; }
            if (!armed) { armed = true; since = now; Ready = false; }
            if (unchecked(now - since) >= 5000u) Ready = true;
        }
    }

    public partial class Form1
    {
        private readonly SecretCreditGate secretCreditGate = new SecretCreditGate();
        private SearchItem secretCreditItem;

        private void UpdateSecretCredits()
        {
            bool before = secretCreditGate.Ready;
            secretCreditGate.Update(searchOpen, searchOverlay == null ? null : searchOverlay.Escrito, unchecked((uint)Environment.TickCount));
            if (searchOverlay == null) return;
            if (!searchOpen || !SecretCreditGate.Matches(searchOverlay.Escrito))
            {
                if (secretCreditItem != null)
                {
                    searchOverlay.Results = new List<SearchItem>();
                    searchOverlay.DatosVer++;
                    searchOverlay.Invalidate();
                    secretCreditItem = null;
                }
                return;
            }
            if (!secretCreditGate.Ready && secretCreditItem == null)
            {
                if (searchOverlay.Results != null && searchOverlay.Results.Count > 0)
                { searchOverlay.Results = new List<SearchItem>(); searchOverlay.DatosVer++; searchOverlay.Invalidate(); }
                return;
            }
            if (secretCreditGate.Ready && (!before || secretCreditItem == null))
            {
                secretCreditItem = new SearchItem { Kind = 3, Display = "+4 cr\u00e9ditos", Key = "" };
                searchOverlay.Results = new List<SearchItem> { secretCreditItem };
                searchOverlay.DatosVer++;
                searchOverlay.FocusResults = true; searchOverlay.SelResult = 0;
                searchOverlay.Invalidate();
            }
        }

        private void GrantSecretCredits(SearchItem item)
        {
            // Validar de nuevo: una tarjeta vieja no debe funcionar tras borrar/cerrar.
            UpdateSecretCredits();
            if (!secretCreditGate.Ready || !object.ReferenceEquals(item, secretCreditItem) ||
                startupReadPending || startupClosing || libBuilding || !stateRestored) return;
            if (credits > int.MaxValue - 4) return;
            credits += 4;
            if (pastCred != null) { pastCred.Moneda(); pastCred.Invalidate(); }
            UpdateCredits();
            SaveState();   // misma persistencia inmediata que los creditos normales
            // No aumentar monedas ni ingresos: son creditos de servicio.
            ShowSearchToast("+4 cr\u00e9ditos", true);
        }
    }
}
