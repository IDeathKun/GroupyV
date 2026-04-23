using System;
using GroupyV.Models;

namespace GroupyV.Services
{
    public class UserSession
    {
        private static UserSession _instance;
        public static UserSession Instance => _instance ??= new UserSession();

        public Utilisateur CurrentUser { get; private set; }
        public DateTime LoginTime { get; private set; }
        public string Role { get; private set; }

        private UserSession() { }

        public void StartSession(Utilisateur user)
        {
            CurrentUser = user;
            LoginTime = DateTime.Now;
            Role = "Session Connectée"; // Ou dynamique selon BDD
        }

        public void EndSession()
        {
            CurrentUser = null;
            Role = null;
            LoginTime = default;
        }

        public string GetNomComplet() => CurrentUser != null ? $"{CurrentUser.Prenom} {CurrentUser.Nom}" : "Invité";

        public string GetInitiales()
        {
            if (CurrentUser == null) return "??";
            string p = string.IsNullOrEmpty(CurrentUser.Prenom) ? "" : CurrentUser.Prenom.Substring(0, 1);
            string n = string.IsNullOrEmpty(CurrentUser.Nom) ? "" : CurrentUser.Nom.Substring(0, 1);
            return (p + n).ToUpper();
        }
    }
}