using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PartnerCapitalInterestCalculator
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }

    public enum CompoundingFrequency
    {
        Monthly,
        Yearly,
        Weekly,
        Daily
    }

    public class CapitalEvent
    {
        public string Partner { get; set; }
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public string Note { get; set; }
    }

    public class MainForm : Form
    {
        private DataGridView grid;
        private Button btnAddRow, btnCalculate, btnExportCsv, btnClear;
        private ComboBox cmbFrequency;
        private NumericUpDown nudRate;
        private DateTimePicker dtpFinancialYearEnd;
        private Label lblRate, lblFrequency, lblFYEnd;
        public MainForm()
        {
            Text = "Partner Capital Interest Calculator (calculate to 31st March)";
            Width = 1100;
            Height = 650;

            grid = new DataGridView
            {
                Dock = DockStyle.Top,
                Height = 420,
                AllowUserToAddRows = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            };

            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Partner", HeaderText = "Partner Name" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Date", HeaderText = "Date (yyyy-MM-dd)" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Amount", HeaderText = "Amount (₹)" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Note", HeaderText = "Note (e.g. initial / add / reinvest)" });

            Controls.Add(grid);

            var panel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(10),
                AutoScroll = true
            };

            lblRate = new Label { Text = "Annual Rate %:", AutoSize = true, Margin = new Padding(5, 12, 5, 5) };
            nudRate = new NumericUpDown { Width = 80, DecimalPlaces = 4, Value = 12, Minimum = 0, Maximum = 100, Increment = 0.1M };

            lblFrequency = new Label { Text = "Compounding:", AutoSize = true, Margin = new Padding(15, 12, 5, 5) };
            cmbFrequency = new ComboBox { Width = 120, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbFrequency.Items.AddRange(new string[] { "Monthly", "Yearly", "Weekly", "Daily" });
            cmbFrequency.SelectedIndex = 0;

            lblFYEnd = new Label { Text = "Financial Year End (31-Mar):", AutoSize = true, Margin = new Padding(15, 12, 5, 5) };
            dtpFinancialYearEnd = new DateTimePicker { Width = 140, Format = DateTimePickerFormat.Custom, CustomFormat = "yyyy-MM-dd", Value = new DateTime(DateTime.Today.Year, 3, 31) };

            btnAddRow = new Button { Text = "Add Row", AutoSize = true };
            btnAddRow.Click += (s, e) => AddEmptyRow();

            btnCalculate = new Button { Text = "Calculate", AutoSize = true };
            btnCalculate.Click += (s, e) => CalculateAll();

            btnExportCsv = new Button { Text = "Export CSV", AutoSize = true };
            btnExportCsv.Click += (s, e) => ExportCsv();

            btnClear = new Button { Text = "Clear", AutoSize = true };
            btnClear.Click += (s, e) => grid.Rows.Clear();

            panel.Controls.Add(lblRate);
            panel.Controls.Add(nudRate);
            panel.Controls.Add(lblFrequency);
            panel.Controls.Add(cmbFrequency);
            panel.Controls.Add(lblFYEnd);
            panel.Controls.Add(dtpFinancialYearEnd);
            panel.Controls.Add(btnAddRow);
            panel.Controls.Add(btnCalculate);
            panel.Controls.Add(btnExportCsv);
            panel.Controls.Add(btnClear);

            Controls.Add(panel);

            // sample rows
            for (int i = 0; i < 4; i++) AddEmptyRow();
        }

        private void AddEmptyRow()
        {
            grid.Rows.Add("", DateTime.Today.ToString("yyyy-MM-dd"), "0.00", "");
        }

        private void CalculateAll()
        {
            try
            {
                // Gather events
                var events = new List<CapitalEvent>();
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.IsNewRow) continue;
                    string partner = Convert.ToString(row.Cells["Partner"].Value)?.Trim() ?? "";
                    if (string.IsNullOrEmpty(partner)) continue;

                    if (!DateTime.TryParse(Convert.ToString(row.Cells["Date"].Value), out DateTime dt))
                        throw new Exception($"Invalid date for partner {partner}.");

                    if (!decimal.TryParse(Convert.ToString(row.Cells["Amount"].Value), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal amt) || amt == 0)
                        throw new Exception($"Invalid amount for partner {partner}.");

                    string note = Convert.ToString(row.Cells["Note"].Value) ?? "";
                    events.Add(new CapitalEvent { Partner = partner, Date = dt.Date, Amount = amt, Note = note });
                }

                if (events.Count == 0) { MessageBox.Show("No valid capital events entered."); return; }

                // Determine financial year end: user provided date's year/03/31 used as FY end; but ensure it's Mar 31.
                DateTime fyEnd = dtpFinancialYearEnd.Value;
                // Force day/month to 31-Mar of that year
                fyEnd = new DateTime(fyEnd.Year, 3, 31);

                var freq = (CompoundingFrequency)cmbFrequency.SelectedIndex;
                decimal annualRate = nudRate.Value / 100m;

                // Group events by partner
                var byPartner = events.GroupBy(e => e.Partner, StringComparer.OrdinalIgnoreCase);

                // Prepare output table
                var results = new List<(string Partner, decimal PrincipalSum, decimal Interest, decimal FinalAmount)>();

                foreach (var g in byPartner)
                {
                    decimal principalSum = g.Sum(x => x.Amount);
                    decimal finalSum = 0m;

                    foreach (var ev in g)
                    {
                        if (ev.Date > fyEnd)
                        {
                            // event after FY end -> ignored
                            continue;
                        }
                        DateTime from = ev.Date;
                        DateTime to = fyEnd;

                        decimal amountFinal = CompoundAmount(ev.Amount, annualRate, freq, from, to);
                        finalSum += amountFinal;
                    }

                    decimal interest = finalSum - principalSum;
                    results.Add((g.Key, principalSum, Math.Round(interest, 2), Math.Round(finalSum, 2)));
                }

                // Show results in a new form with grid
                ShowResults(results, annualRate, freq, fyEnd);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Calculation error: " + ex.Message);
            }
        }

        private void ShowResults(List<(string Partner, decimal PrincipalSum, decimal Interest, decimal FinalAmount)> results, decimal annualRate, CompoundingFrequency freq, DateTime fyEnd)
        {
            var f = new Form { Text = $"Results to {fyEnd:yyyy-MM-dd}", Width = 700, Height = 500 };
            var g = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            g.Columns.Add("Partner", "Partner");
            g.Columns.Add("Principal", "Principal (₹)");
            g.Columns.Add("Interest", "Interest (₹)");
            g.Columns.Add("Final", "Final Amount (₹)");

            foreach (var r in results)
            {
                g.Rows.Add(r.Partner, r.PrincipalSum.ToString("F2"), r.Interest.ToString("F2"), r.FinalAmount.ToString("F2"));
            }

            var lbl = new Label { Text = $"Annual Rate: {annualRate:P2}    Compounding: {freq}    FY End: {fyEnd:yyyy-MM-dd}", Dock = DockStyle.Top, Height = 30 };
            f.Controls.Add(g);
            f.Controls.Add(lbl);
            f.ShowDialog();
        }

        private decimal CompoundAmount(decimal principal, decimal annualRate, CompoundingFrequency freq, DateTime from, DateTime to)
        {
            if (to < from) return principal;

            switch (freq)
            {
                case CompoundingFrequency.Yearly:
                    return CompoundByYears(principal, annualRate, from, to);
                case CompoundingFrequency.Monthly:
                    return CompoundByMonths(principal, annualRate, from, to);
                case CompoundingFrequency.Weekly:
                    return CompoundByFixedPeriod(principal, annualRate, from, to, TimeSpan.FromDays(7));
                case CompoundingFrequency.Daily:
                    return CompoundByFixedPeriod(principal, annualRate, from, to, TimeSpan.FromDays(1));
                default:
                    return CompoundByMonths(principal, annualRate, from, to);
            }
        }

        private decimal CompoundByYears(decimal principal, decimal annualRate, DateTime from, DateTime to)
        {
            int years = 0;
            DateTime cursor = from;
            while (true)
            {
                var next = cursor.AddYears(1);
                if (next <= to) { years++; cursor = next; } else break;
            }
            decimal yearlyRate = annualRate;
            decimal amountAfterYears = principal * PowDecimal(1m + yearlyRate, years);
            int extraDays = (to - cursor).Days;
            if (extraDays > 0)
            {
                decimal dailyRate = yearlyRate / 365m;
                amountAfterYears *= (1 + dailyRate * extraDays);
            }
            return amountAfterYears;
        }

        private decimal CompoundByMonths(decimal principal, decimal annualRate, DateTime from, DateTime to)
        {
            int months = 0;
            DateTime cursor = from;
            while (true)
            {
                DateTime next = AddOneMonthPreservingDay(cursor);
                if (next <= to) { months++; cursor = next; } else break;
            }
            decimal monthlyRate = annualRate / 12m;
            decimal amountAfterMonths = principal * PowDecimal(1m + monthlyRate, months);
            int extraDays = (to - cursor).Days;
            if (extraDays > 0)
            {
                decimal dailyRate = monthlyRate / 30m; // 30-day convention for leftover
                amountAfterMonths *= (1 + dailyRate * extraDays);
            }
            return amountAfterMonths;
        }

        private decimal CompoundByFixedPeriod(decimal principal, decimal annualRate, DateTime from, DateTime to, TimeSpan period)
        {
            int periods = 0;
            DateTime cursor = from;
            while (true)
            {
                var next = cursor + period;
                if (next <= to) { periods++; cursor = next; } else break;
            }
            decimal periodRate = annualRate * (decimal)(period.TotalDays / 365.0);
            decimal amountAfterPeriods = principal * PowDecimal(1m + periodRate, periods);
            int extraDays = (to - cursor).Days;
            if (extraDays > 0)
            {
                decimal dailyRate = (annualRate / 365m);
                amountAfterPeriods *= (1 + dailyRate * extraDays);
            }
            return amountAfterPeriods;
        }

        private static DateTime AddOneMonthPreservingDay(DateTime dt)
        {
            int year = dt.Year;
            int month = dt.Month + 1;
            if (month > 12) { month = 1; year++; }
            int day = Math.Min(dt.Day, DateTime.DaysInMonth(year, month));
            return new DateTime(year, month, day);
        }

        private static decimal PowDecimal(decimal x, int n)
        {
            if (n == 0) return 1m;
            decimal result = 1m;
            decimal baseVal = x;
            int exp = n;
            while (exp > 0)
            {
                if ((exp & 1) == 1) result *= baseVal;
                baseVal *= baseVal;
                exp >>= 1;
            }
            return result;
        }

        private void ExportCsv()
        {
            if (grid.Rows.Count == 0) { MessageBox.Show("No data to export."); return; }
            using (var sfd = new SaveFileDialog { Filter = "CSV Files (*.csv)|*.csv", FileName = "CapitalEvents.csv" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    try
                    {
                        using (var sw = new StreamWriter(sfd.FileName))
                        {
                            sw.WriteLine("Partner,Date,Amount,Note");
                            foreach (DataGridViewRow row in grid.Rows)
                            {
                                if (row.IsNewRow) continue;
                                var cells = new string[]
                                {
                                    EscapeCsv(row.Cells["Partner"].Value),
                                    EscapeCsv(row.Cells["Date"].Value),
                                    EscapeCsv(row.Cells["Amount"].Value),
                                    EscapeCsv(row.Cells["Note"].Value)
                                };
                                sw.WriteLine(string.Join(",", cells));
                            }
                        }
                        MessageBox.Show("CSV exported.");
                    }
                    catch (Exception ex) { MessageBox.Show("Export failed: " + ex.Message); }
                }
            }
        }

        private string EscapeCsv(object value)
        {
            string s = value == null ? "" : Convert.ToString(value);
            if (s.Contains('"') || s.Contains(',') || s.Contains('\n')) s = "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}