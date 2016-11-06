using System;
using System.Collections.Generic;
using System.Data.OleDb;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Siedle.Prs602.RepairTool
{
    public class SiedleDatabaseManager
    {
        private readonly OleDbConnection _connection;
        private readonly TextWriter _logger;

        public SiedleDatabaseManager(string fileName, TextWriter logger)
        {
            _connection = new OleDbConnection();
            _connection.ConnectionString = "Provider=Microsoft.Jet.OLEDB.4.0; Data source=" + fileName + ";";
            _logger = logger;
        }

        public void TrimTexts()
        {
            _logger.WriteLine("Fixing texts...");
            _connection.Open();
            try
            {
                var cardsToTrim = EcCard.LoadAll(_connection)
                    .Where(c => c.Text != null && c.Text != c.Text.Trim())
                    .ToList();
                foreach (var card in cardsToTrim)
                {
                    _logger.WriteLine("{0}", card);
                    card.Text = card.Text.Trim();
                    card.Save(_connection);
                }
            }
            finally
            {
                _connection.Close();
            }
        }

        public void TestFlagsValidity()
        {
            _logger.WriteLine("Testing card validity...");
            _connection.Open();
            try
            {
                var invalidFlags = EcCard.LoadAll(_connection)
                    .Where(c => !c.IsFlagsValid)
                    .ToList();
                foreach (var card in invalidFlags)
                {
                    _logger.WriteLine("{0}", card);
                    _logger.Write("     flags:");
                    foreach (var flag in card.Flags)
                    {
                        _logger.Write(" {0}", flag ? "1" : "0");
                    }
                    _logger.Write("  expected:");
                    foreach (var flag in EcCard.ExpectedFlags(card.ProjectNumber))
                    {
                        _logger.Write(" {0}", flag ? "1" : "0");
                    }
                    _logger.WriteLine();
                }
            }
            finally
            {
                _connection.Close();
            }
        }

        public void CreateMissingCards()
        {
            _logger.WriteLine("Creating missing cards...");
            _connection.Open();
            try
            {
                var allCards = EcCard.LoadAll(_connection).ToList();

                var cardIds = new HashSet<string>();
                foreach (var c in allCards)
                    cardIds.Add(c.CardIdentity);

                foreach (var id in cardIds)
                {
                    bool unique = true;
                    var cards = allCards.Where(c => string.Equals(c.CardIdentity, id)).ToList();
                    if (cards.Count != 3)
                    {
                        _logger.WriteLine("{0} have {1} entries", id, cards.Count);
                    }

                    if (cards.Any(c => c.CardNumber != cards[0].CardNumber))
                    {
                        unique = false;
                        _logger.WriteLine("{0} have different card numbers: {1}", id, string.Join(", ", cards.Select(c => c.CardNumber)));
                    }

                    if (cards.Any(c => c.CustomerNumber != cards[0].CustomerNumber))
                    {
                        unique = false;
                        _logger.WriteLine("{0} have different customer numbers: {1}", id, string.Join(", ", cards.Select(c => c.CustomerNumber)));
                    }

                    if (unique && cards.Count < 3)
                    {
                        foreach (var projectId in EcCard.ProjectIds)
                        {
                            if (cards.Any(c => c.ProjectNumber == projectId))
                                continue;

                            var card = EcCard.Create(cards[0].CustomerNumber, projectId, 
                                cards[0].CardNumber, cards[0].CardIdentity);
                            card.Text = cards[0].Text;
                            card.SetFlags(cards[0].IsActive);
                            _logger.WriteLine("Creating new card {0}", card);
                            card.Save(_connection);
                        }
                    }
                }
            }
            finally
            {
                _connection.Close();
            }
        }

        public void FixDescriptionTexts()
        {
            _logger.WriteLine("Fixing card text comments...");
            _connection.Open();
            try
            {
                var allCards = EcCard.LoadAll(_connection).ToList();
                var masterCards = allCards.Where(c => c.ProjectNumber == EcCard.ProjectIds[0]).ToList();
                var slaveCards = allCards.Where(c => c.ProjectNumber != EcCard.ProjectIds[0]).ToList();

                _logger.WriteLine("Master unique cards:");
                foreach (var card in masterCards.Where(m => !slaveCards.Any(s => string.Equals(s.CardIdentity, m.CardIdentity))))
                {
                    _logger.WriteLine(card);
                }
                _logger.WriteLine();

                _logger.WriteLine("Slave unique cards:");
                foreach (var card in slaveCards.Where(s => !masterCards.Any(m => string.Equals(s.CardIdentity, m.CardIdentity))))
                {
                    _logger.WriteLine(card);
                }
                _logger.WriteLine();

                foreach (var masterCard in masterCards.Where(m => !string.IsNullOrWhiteSpace(m.Text)))
                {
                    var identity = masterCard.CardIdentity;
                    var slaves = slaveCards.Where(s => string.Equals(s.CardIdentity, identity)).ToList();
                    if (slaves.Any(s => !string.Equals(s.Text, masterCard.Text) && !string.IsNullOrWhiteSpace(s.Text)))
                    {
                        _logger.WriteLine("different texts:");
                        _logger.WriteLine("\t" + masterCard);
                        foreach (var slave in slaves)
                        {
                            _logger.WriteLine("\t" + slave);
                        }
                        continue;
                    }

                    foreach (var slave in slaves.Where(s => string.IsNullOrWhiteSpace(s.Text)))
                    {
                        _logger.WriteLine("Copying '{0}' to card '{1}' in project {2}", masterCard.Text, slave.CardIdentity, slave.ProjectNumber);
                        slave.Text = masterCard.Text;
                        slave.Save(_connection);
                    }
                }
            }
            finally
            {
                _connection.Close();
            }
        }

        public void FindNumberingHoles()
        {
            _logger.WriteLine("Finding card numbering holes...");
            _connection.Open();
            try
            {
                var allCards = EcCard.LoadAll(_connection).ToList();

                int cardIndex = 0;
                foreach (var card in allCards.OrderBy(c => c.CardNumber))
                {
                    if (card.CardNumber > cardIndex + 1)
                    {
                        _logger.WriteLine("No card(s) at position {0}-{1}", cardIndex + 1, card.CardNumber);
                    }
                    cardIndex = card.CardNumber;
                }
            }
            finally
            {
                _connection.Close();
            }
        }
    }
}
