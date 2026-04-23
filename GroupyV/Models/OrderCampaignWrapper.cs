using System;
using System.Collections.Generic;
using GroupyV.Models;

namespace GroupyV.Models
{
    public class OrderCampaignWrapper
    {
        public Prevente Prevente { get; set; }
        public List<Participation> Participants { get; set; }
        public Expedition ExpeditionInfo { get; set; }

        public bool IsShipped => ExpeditionInfo != null;

        public decimal TotalRevenue => Prevente != null && Participants != null ? Prevente.PrixPrevente * Participants.Count : 0;

        public string StatusText => IsShipped ? "EXPÉDIÉE" : "EN ATTENTE";

        public string StatusColor
        {
            get
            {
                if (IsShipped) return "#00E676"; // Vert
                if (IsLate) return "#FF5252";    // Rouge
                return "#FF9100";                // Orange
            }
        }

        public bool IsLate
        {
            get
            {
                if (IsShipped) return false;
                if (Prevente.DateLimite.HasValue)
                {
                    // Alerte si la date limite est passée de plus de 48h
                    return DateTime.Now > Prevente.DateLimite.Value.AddDays(2);
                }
                return false;
            }
        }

        public int TotalParcels => Participants.Count;
    }
}