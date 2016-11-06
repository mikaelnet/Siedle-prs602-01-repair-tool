using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.OleDb;
using System.Linq;
using System.Text;

namespace Siedle.Prs602.RepairTool
{
    public class EcCard
    {
        public int ID { get; set; }
        public int CustomerNumber { get; set; }
        public int ProjectNumber { get; set; }
        public int CardNumber { get; set; }
        public string CardIdentity { get; set; }
        public string Text { get; set; }

        private readonly bool[] _flags;

        public static readonly string[] FlagNames = {
            "TO1", "TO2", "TO3", "TO4", "TO5", "TO6", "TO7", "TO8",
            "SO1", "SO2", "SO3", "SO4", "Zeitg"
        };

        public static readonly int[] ProjectIds = {2, 3, 4};

        private static readonly bool[,] ProjectFlags;

        private static readonly string InsertCommand;
        private static readonly string UpdateCommand;

        static EcCard()
        {
            ProjectFlags = new bool[ProjectIds.Length,FlagNames.Length];

            // Project 2 & 3: TO8, SO3, SO4, Zeitg false
            // Project 4:  TO8, Zeitg false
            // All other should be true when active
            for (int i = 0; i < ProjectIds.Length; i++)
            {
                for (int j = 0; j < FlagNames.Length; j++)
                {
                    var name = FlagNames[j];
                    var proj = ProjectIds[i];
                    if (name == "TO8" || name == "Zeitg")
                    {
                        ProjectFlags[i, j] = false;
                    }
                    else if ((name == "SO3" || name == "SO4") && (proj == 2 || proj == 3))
                    {
                        ProjectFlags[i, j] = false;
                    }
                    else
                    {
                        ProjectFlags[i, j] = true;
                    }
                }
            }

            for (int i = 0; i < ProjectIds.Length; i++)
            {
                ProjectFlags[i, 7] = false;
                ProjectFlags[i, 12] = false;
            }
            for (int i = 0; i < 2; i++)
            {
                ProjectFlags[i, 7] = false;
                ProjectFlags[i, 12] = false;
            }

            var sb = new StringBuilder("INSERT INTO [ECCards] ([KndNr], [ProjNr], [Kartennr], [CardZK], [Text]");
            foreach (var flagName in FlagNames)
            {
                sb.AppendFormat(", [{0}]", flagName);
            }
            sb.Append(") VALUES (@CustomerNumber, @ProjectNumber, @CardNumber, @CardIdentity, @Text");
            foreach (var flagName in FlagNames)
            {
                sb.AppendFormat(", @{0}", flagName);
            }
            sb.Append(")");
            InsertCommand = sb.ToString();

            sb = new StringBuilder("UPDATE [ECCards] SET [KndNr] = @CustomerNumber, [ProjNr] = @ProjectNumber, [Kartennr] = @CardNumber, [CardZK] = @CardIdentity, [Text] = @Text");
            foreach (var flagName in FlagNames)
            {
                sb.AppendFormat(", [{0}] = @{0}", flagName);
            }
            sb.Append(" WHERE [ID] = @ID");
            UpdateCommand = sb.ToString();
        }

        private EcCard()
        {
            _flags = new bool[FlagNames.Length];
        }

        public static EcCard Create(int customerNumber, int projectNumber, int cardNumber, string cardIdentity)
        {
            var card = new EcCard();
            card.CustomerNumber = customerNumber;
            card.ProjectNumber = projectNumber;
            card.CardNumber = cardNumber;
            card.CardIdentity = cardIdentity;
            return card;
        }

        private static int GetProjectIndex(int projectNumber)
        {
            var projectIndex = -1;
            for (int i = 0; i < ProjectIds.Length; i++)
            {
                if (ProjectIds[i] == projectNumber)
                {
                    projectIndex = i;
                    break;
                }
            }
            if (projectIndex < 0 || projectIndex > ProjectIds.Length)
                throw new ArgumentException("Invalid project number " + projectNumber);

            return projectIndex;
        }

        public bool IsFlagsValid
        {
            get
            {
                if (_flags.All(f => f == false))
                    return true;
                var projectIndex = GetProjectIndex (ProjectNumber);

                return !_flags.Where((t, i) => t != ProjectFlags[projectIndex, i]).Any();
            }
        }

        public bool IsActive
        {
            get
            {
                return _flags.Any(f => f);
            }
        }

        public IEnumerable<bool> Flags => _flags;

        public static IEnumerable<bool> ExpectedFlags(int projectNumber)
        {
            int projectIndex = GetProjectIndex(projectNumber);
            for (int i = 0; i < FlagNames.Length; i ++)
            {
                yield return ProjectFlags[projectIndex, i];
            }
        }

        public void SetFlags(bool state)
        {
            if (state)
            {
                var projectIndex = ProjectNumber - 2;
                if (projectIndex < 0 || projectIndex > 2)
                    throw new ArgumentException("Invalid project number " + ProjectNumber);

                for (int i = 0; i < _flags.Length; i++)
                    _flags[i] = ProjectFlags[projectIndex, i];
            }
            else
            {
                for (int i = 0; i < _flags.Length; i++)
                    _flags[i] = false;
            }
        }

        private static IEnumerable<EcCard> LoadFromReader(OleDbDataReader reader)
        {
            if (reader == null)
                yield break;

            int idIndex = reader.GetOrdinal("ID");
            int custIndex = reader.GetOrdinal("KndNr");
            int projIndex = reader.GetOrdinal("ProjNr");
            int cardNoIndex = reader.GetOrdinal("Kartennr");
            int cardIdIndex = reader.GetOrdinal("CardZK");
            int textIndex = reader.GetOrdinal("Text");
            int[] flagIndexes = new int[13];
            for (int i = 0; i < FlagNames.Length; i++)
                flagIndexes[i] = reader.GetOrdinal(FlagNames[i]);

            while (reader.Read())
            {
                var card = new EcCard();
                card.ID = reader.GetInt32(idIndex);
                card.CustomerNumber = (int)reader.GetDouble(custIndex);
                card.ProjectNumber = (int)reader.GetDouble(projIndex);
                card.CardNumber = reader.GetInt16(cardNoIndex);
                card.CardIdentity = reader.GetString(cardIdIndex);
                card.Text = reader.GetValue(textIndex) as string;
                for (int i = 0; i < FlagNames.Length; i++)
                {
                    card._flags[i] = reader.GetBoolean(flagIndexes[i]);
                }
                yield return card;
            }
        }

        public static IEnumerable<EcCard> LoadAll(OleDbConnection connection)
        {
            var command = new OleDbCommand("select * from ECCards", connection);
            var reader = command.ExecuteReader();
            return LoadFromReader(reader);
        }

        public void Save(OleDbConnection connection)
        {
            var command = new OleDbCommand(ID > 0 ? UpdateCommand : InsertCommand, connection);
            command.Parameters.Add(new OleDbParameter("@CustomerNumber", CustomerNumber));
            command.Parameters.Add(new OleDbParameter("@ProjectNumber", ProjectNumber));
            command.Parameters.Add(new OleDbParameter("@CardNumber", CardNumber));
            command.Parameters.Add(new OleDbParameter("@CardIdentity", CardIdentity));
            command.Parameters.Add(new OleDbParameter("@Text", Text));
            for (int i = 0 ; i < FlagNames.Length ; i ++) 
            {
                command.Parameters.Add(new OleDbParameter($"@{FlagNames[i]}", _flags[i]));
            }

            if (ID > 0)
            {
                command.Parameters.Add(new OleDbParameter("@ID", ID));
            }

            command.ExecuteNonQuery();
        }

        public void Delete(OleDbConnection connection)
        {
            if (ID > 0)
            {
                var command = new OleDbCommand("DELETE FROM [ECCards] WHERE [ID] = @ID", connection);
                command.Parameters.Add(new OleDbParameter("@ID", ID));
                command.ExecuteNonQuery();
            }
        }

        public override string ToString()
        {
            return string.Join("\t", ProjectNumber-1, CardNumber, $"({ID})", CardIdentity, Text);
        }
    }
}