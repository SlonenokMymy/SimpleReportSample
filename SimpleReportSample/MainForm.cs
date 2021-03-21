﻿using ClosedXML.Excel;
using ExcelReportGenerator.Rendering;
using SimpleReportSample.Reports;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Office.Interop.Excel;
using iText.Kernel.Pdf;
using System.Collections.Generic;
using System.Linq;
using SimpleReportSample.Extensions;
using SimpleReportSample.HelperClassesAndInterfaces;
using SimpleReportSample.HelperClassesAndInterfaces.BaseInterfaces;
using System.Web.UI.WebControls;
using System.Drawing;
using CheckBox = System.Windows.Forms.CheckBox;

namespace SimpleReportSample
{
    public partial class MainForm : Form
    {
        private readonly DataProvider _dataProvider = new DataProvider();

        private List<ContractorsAndContracts> _contractorsAndContractsData = new List<ContractorsAndContracts>();

        public List<PaymentData> _paymentData = new List<PaymentData>();

        private List<string> _timeReportFilesList = new List<string>();

        private string _invoiceReportFolder = "Invoice_Report";

        private string _reportName = "_InvoiceSpecAcceptanceTemplate";

        public MainForm()
        {
            InitializeComponent();
        }

        private async void ActionButton_Click(object sender, EventArgs e)
        {
        }

        private static XLWorkbook GetReportTemplateWorkbook(string reportName = "InvoiceTemplate")
        {
            return new XLWorkbook(Path.Combine("Reports", "Templates", $"{reportName}.xlsx"));
        }

        private static DefaultReportGenerator GetInvoiceReportGenerator(InvoiceReportParams invoiceReportParams)
        {
            return new DefaultReportGenerator(new InvoiceReport(invoiceReportParams));
        }

        private void SelectPathToContractorsAndContracts_Click(object sender, EventArgs e)
        {
            PathToContractorsAndContracts.Text = GetPathFromOpenFileDialog();
            _contractorsAndContractsData = _dataProvider.GetContractorsAndContractsTableData(PathToContractorsAndContracts.Text, "Контракты");

            var data = _contractorsAndContractsData
                .Select(x => new Employee() 
                { 
                    EmployeeName = x.NameEng,
                    TimeReportDocExists = true
                })
                .ToList();

            BindTable(data);
        }

        private class Employee
        { 
            public string EmployeeName { get; set; }

            public bool TimeReportDocExists { get; set; }
        }


        private void BindTable(List<Employee> employees)
        {
            EmploeeGridView.DataSource = employees;
        }

        private void SelectPathToPaymentsDoc_Click(object sender, EventArgs e)
        {
            PathToPaymentsDoc.Text = GetPathFromOpenFileDialog();

            _paymentData = _dataProvider.GetPaymentTableData(PathToPaymentsDoc.Text, "Payments");
        }

        private void SelectPathToTaskSummaryDoc_Click(object sender, EventArgs e)
        {
            string path = OpenFolderDialog();
            PathToTaskSummaryDoc.Text = path;

            _timeReportFilesList = Directory.GetFiles(@path, "*.xlsx"). ToList();
        }

        private string GetPathFromOpenFileDialog()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "All Files (*.*)|*.*";
            openFileDialog.FilterIndex = 1;

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                return openFileDialog.FileName;
            }

            return string.Empty;
        }

        private string OpenFolderDialog()
        {
            using (var fbd = new FolderBrowserDialog())
            {
                DialogResult result = fbd.ShowDialog();

                if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
                {
                    return fbd.SelectedPath;
                }
            }

            return string.Empty;
        }

        private void SaveToPDF_Click(object sender, EventArgs e)
        {
            List<string> employeesForInvoiceReportList = new List<string>();
            List<string> noTimeReportEmploees = new List<string>();

            foreach (DataGridViewRow row in this.EmploeeGridView.Rows)
            {
                if (object.Equals(row.Cells.GetCellValueFromColumnHeader("CreateInvoiceFiles"), true))
                    employeesForInvoiceReportList.Add(row.Cells.GetCellValueFromColumnHeader("EmployeeName").ToString());
            }

            if (!employeesForInvoiceReportList.Any())
                throw new Exception("Не выбрано ни одной строчки для генерации Invoice Reports");

            SetProgressBar(employeesForInvoiceReportList);
                
            foreach (var employee in employeesForInvoiceReportList)
            {
                GenerateReportProgressBar.PerformStep();
                //ProgressBarLabel.Text = $"Обработка файла #{GenerateReportProgressBar.Step} {employee}";
                var pathToEmployee = GetPathToTimeReportByEmployeeName(PathToTaskSummaryDoc.Text, employee);

                if (string.IsNullOrEmpty(pathToEmployee))
                {
                    noTimeReportEmploees.Add(employee);
                    continue;
                }

                var reportGenerator = GetInvoiceReportGenerator(new InvoiceReportParams()
                {
                    ContractorsData = GetContractorsData(employee),
                    PaymentData = GetPaymentDataFromList(employee),
                    PathToTaskSummaryDocs = pathToEmployee,
                    DateFrom = DateTimeFrom.Value,
                    DateTo = DateTimeTo.Value
                });

                GenerateReport(reportGenerator, employee.Replace(' ', '_'));
            }

            UpdateDataGrid(noTimeReportEmploees);
            UpdateProgressBarAndGridview();
        }

        private void UpdateProgressBarAndGridview()
        {
            GenerateReportProgressBar.Visible = false;
            ProgressBarLabel.Visible = false;
            ProgressBarLabel.Text = string.Empty;
            EmploeeGridView.Enabled = true;
        }

        private void SetProgressBar(List<string> employeesForInvoiceReportList)
        {
            EmploeeGridView.Enabled = false;
            GenerateReportProgressBar.Maximum = employeesForInvoiceReportList.Count;
            GenerateReportProgressBar.Minimum = 1;
            GenerateReportProgressBar.Step = 1;
            GenerateReportProgressBar.Visible = true;
            ProgressBarLabel.Visible = true;
        }

        private void UpdateDataGrid(List<string> emploees)
        {
            foreach (DataGridViewRow row in EmploeeGridView.Rows)
                if (emploees.Contains(row.Cells[1].Value.ToString()))
                {
                    row.DefaultCellStyle.BackColor = Color.Red;
                }
        }

        private PaymentData GetPaymentDataFromList(string employee)
        {
            string[] emploeeNameDivided = employee.Split(' ');

            return _paymentData.Where(x => x.EmployerName.Contains(emploeeNameDivided[0]) && x.EmployerName.Contains(emploeeNameDivided[1])).SingleOrDefault();
        }

        private string GetPathToTimeReportByEmployeeName(string pathToTaskSummaryDocText, string employeeName)
        {
            string[] emploeeNameDivided = employeeName.Split(' ');

            ///добавить фильтр по дате из названия файла
            return _timeReportFilesList.Where(x => x.Contains(emploeeNameDivided[0]) && x.Contains(emploeeNameDivided[1])).SingleOrDefault();
        }

        private ContractorsAndContracts GetContractorsData(string employee)
        {
            return _contractorsAndContractsData.Where(x => string.Equals(x.NameEng, employee)).Single();
        }

        private void GenerateReport(DefaultReportGenerator reportGenerator, string employee)
        {
            try
            {
                var result = reportGenerator.Render(GetReportTemplateWorkbook());

                result.SaveAs(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), _invoiceReportFolder, string.Format($"{employee}{_reportName}.xlsx")));

                Microsoft.Office.Interop.Excel.Application app = new Microsoft.Office.Interop.Excel.Application();
                Workbook excelWorkbook = app.Workbooks.Open(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), _invoiceReportFolder, string.Format($"{employee}{_reportName}.xlsx")));

                Worksheet worksheetSpecification = (Worksheet)excelWorkbook.Worksheets["Specification"];
                var range = worksheetSpecification.Range["C:D"];

                range.Rows.AutoFit();

                worksheetSpecification.PageSetup.Orientation = XlPageOrientation.xlPortrait;
                worksheetSpecification.PageSetup.Zoom = false;
                worksheetSpecification.PageSetup.FitToPagesWide = 1;
                worksheetSpecification.PageSetup.FitToPagesTall = 1;
                worksheetSpecification.PageSetup.PaperSize = XlPaperSize.xlPaperA4;

                excelWorkbook.ExportAsFixedFormat(XlFixedFormatType.xlTypePDF, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), _invoiceReportFolder, string.Format($"{employee}{_reportName}_Temp.pdf")),
                    XlFixedFormatQuality.xlQualityMinimum, true, true, 1, 4, false);

                PdfDocument pdfDocument = new PdfDocument(new PdfReader(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), _invoiceReportFolder, string.Format($"{employee}{_reportName}_Temp.pdf"))),
                    new PdfWriter(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), _invoiceReportFolder, string.Format($"{employee}{_reportName}.pdf"))));

                ///Note that as you remove a page the number of pages in your PDF will change
                pdfDocument.RemovePage(1);

                pdfDocument.Close();
                excelWorkbook.Close(SaveChanges: false);

                File.Delete(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), _invoiceReportFolder, string.Format($"{employee}{_reportName}_Temp.pdf")));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while running report: {ex.GetBaseException().Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SelectAllRows_Click(object sender, EventArgs e)
        {
            foreach (DataGridViewRow row in EmploeeGridView.Rows)
            {
                ((DataGridViewCheckBoxCell)row.Cells["CreateInvoiceFiles"]).Value = true;
            }

            EmploeeGridView.RefreshEdit();
        }
    }
}