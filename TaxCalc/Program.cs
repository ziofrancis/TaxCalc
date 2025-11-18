using System.Globalization;

namespace TaxCalc
{
    internal class Program
    {
        // ===== GLOBAL STATE =====

        // Core salary and tax fields (all yearly values unless stated otherwise)
        static double
            salary,         // salary as entered by user (could be yearly, monthly, etc.)
            salYear,        // salary converted to yearly amount
            incomeTax,      // total income tax
            USC,            // Universal Social Charge
            PRSI,           // Pay Related Social Insurance
            totalTax,       // incomeTax + USC + PRSI
            salNet;         // net salary after all taxes

        // Personal / household flags
        static bool
            isMarried = false,  // married / civil union status
            hasChild = false;   // lone parent (for higher standard rate band)

        // Income tax band configuration and partner’s income
        static double
            bandIncome = 0,                 // effective income band used for 20% tax
            bandIncomeBase = 44000,         // single person base band
            bandIncomeLoneParent = 48000,   // lone parent base band
            bandIncomeMarried = 53000,      // married base band (one income)
            partnerIncome = 0,              // partner’s income (for two-income married band)
            partnerCap = 35000;             // max extra band from partner income

        // USC / PRSI modifiers
        static int
            userAge = 0;        // used for USC reduced rates (over 70 etc.)

        static bool
            isSelfEmployed = false, // USC higher top rate for self-employed
            hasMedCard = false;     // USC reduced rate with medical card

        // USC thresholds and base rates (can be overridden per-person)
        static int[] thresholdUSC = { 12012, 28700, 70044, 100000 };
        static double[] ratesUSC = { 0.005, 0.02, 0.03, 0.08, 0.08 };

        // PRSI rate change after 1 Oct 2026
        // This is evaluated once at startup to decide which rate to use.
        static readonly bool PRSIswitch = DateTime.Now >= new DateTime(2026, 10, 1);

        // Report state
        static DateTime reportTime = DateTime.Now;
        static bool hasSalary = (salYear > 0);   // becomes true once a salary is calculated
        static int expCount;                     // number of active expense items

        // ===== EXPENSES =====

        const int expMemory = 16;                // max number of expense slots in the table
        static string[] expName = new string[expMemory];    // expense labels
        static double[] expValue = new double[expMemory];   // yearly expense values
        static double expValueTotal, overallBalance;        // totals and final net balance

        // Raw report strings for export (CSV-like form)
        static string rawReport = "", salaryData = "", expensesData = "";

        // Default save path (Desktop) for CSV/TXT exports
        static readonly string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

        // UI helpers
        const int barSize = 80;
        static readonly string dashbar = new string('-', barSize) + "\n";
        static readonly string starbar = new string('*', barSize) + "\n";
        static readonly string blank = "\n";

        static void Main()
        {
            // Ensure correct locale/formatting and Unicode support for box drawing
            Console.OutputEncoding = System.Text.Encoding.Unicode;
            CultureInfo.CurrentCulture = new CultureInfo("en-IE");

            int width = Console.WindowWidth;
            int height = Console.WindowHeight;

            // Warn if console is too small for the fancy layout
            if (width < 80 || height < 30)
            {
                Console.WriteLine($"Warning: This app works best with at least 80x30 terminal.");
                Console.WriteLine($"Current size: {width}x{height}");
                Console.WriteLine("Please resize your terminal and press any key...");
                Console.ReadKey();
                Console.Clear();
            }

            SplashScreen();  // intro screen + disclaimer
            MainMenu();      // main loop
        }

        /*
         * Compose the CSV-like data strings for the report:
         *  - salaryData: salary and taxes
         *  - expensesData: each expense, total expenses, and overall balance
         *
         * Called whenever salary/expenses change to keep rawReport in sync.
         */
        private static void DataComposer()
        {
            salaryData =
                DataLine("Gross salary", salYear) +
                DataLine("Income Tax", incomeTax) +
                DataLine("USC", USC) +
                DataLine("PRSI", PRSI) +
                DataLine("Total taxes", totalTax) +
                DataLine("Net Salary", salNet);

            expensesData = "";
            for (int i = 0; i < expMemory; i++)
            {
                expensesData += DataLine(expName[i], expValue[i]);
            }
            expensesData += DataLine("Total Expenses", expValueTotal);

            overallBalance = salNet - expValueTotal;
            expensesData += DataLine("Overall balance", overallBalance);

            rawReport = "FINANCIAL REPORT,Yearly,Monthly,Weekly,Pctge\n" + salaryData + expensesData;
        }

        /*
         * Draws the initial splash screen with ASCII art and disclaimer.
         * Centered inside a box-drawn frame for a nicer first impression.
         */
        private static void SplashScreen()
        {
            Console.Clear();
            string colour = "g";

            string asciiRaw =
                "Francesco Sannicandro's" +
                "\n\n" +
                " ooooooooooooo                         .oooooo.             oooo            " + "\n" +
                " 8'   888   `8                        d8P'  `Y8b            `888            " + "\n" +
                "      888       .oooo.   oooo    ooo 888           .oooo.    888   .ooooo.  " + "\n" +
                "      888      `P  )88b   `88b..8P'  888          `P  )88b   888  d88' `\"Y8 " + "\n" +
                "      888       .oP\"888     Y888'    888           .oP\"888   888  888       " + "\n" +
                "      888      d8(  888   .o8\"'88b   `88b    ooo  d8(  888   888  888   .o8 " + "\n" +
                "     o888o     `Y888\"\"8o o88'   888o  `Y8bood8P'  `Y888\"\"8o o888o `Y8bod8P' " +
                "\n\n" +
                "Version 1.0.2026" +
                "\n\n\n\n" +
                "DISCLAIMER: For educational purposes only.\n" +
                "Tax calculations may not cover all scenarios.\n" +
                "Always consult official Revenue sources or a tax professional.\n" +
                "This software is provided 'as is' without warranty of any kind." +
                "\n\n\n\n" +
                "Press a key to continue";

            // Split into sections so we can style and center them differently
            string[] lines = asciiRaw.Split("\n");

            string[] aboveAscii = lines[0..2];
            string[] asciiArt = lines[2..10];
            string[] belowAscii = lines[10..14];      // Version and blank lines
            string[] disclaimer = lines[14..18];      // Disclaimer lines
            string[] footer = lines[18..lines.Length];

            int boxWidth = 80;
            int boxHeight = 30;
            int contentHeight = lines.Length;
            int topPadding = (boxHeight - contentHeight) / 2;

            // Top border
            Col("┏" + new string('━', boxWidth - 2) + "┓\n", colour);

            // Top empty padding inside the frame
            for (int i = 0; i < topPadding - 1; i++)
            {
                Col("┃" + new string(' ', boxWidth - 2) + "┃\n", colour);
            }

            // Centered header text
            foreach (string line in aboveAscii)
            {
                int leftPadding = (boxWidth - line.Length) / 2;
                int rightPadding = boxWidth - line.Length - leftPadding;
                Col("┃", colour);
                Col(new string(' ', leftPadding - 1));
                Col(line, "y");
                Col(new string(' ', rightPadding - 1));
                Col("┃\n", colour);
            }

            // Centered ASCII art
            foreach (string line in asciiArt)
            {
                int leftPadding = (boxWidth - line.Length) / 2;
                int rightPadding = boxWidth - line.Length - leftPadding;
                Col("┃", colour);
                Col(new string(' ', leftPadding - 1));
                Col(line, "w");
                Col(new string(' ', rightPadding - 1));
                Col("┃\n", colour);
            }

            // Centered version text
            foreach (string line in belowAscii)
            {
                int leftPadding = (boxWidth - line.Length) / 2;
                int rightPadding = boxWidth - line.Length - leftPadding;
                Col("┃", colour);
                Col(new string(' ', leftPadding - 1));
                Col(line, "y");
                Col(new string(' ', rightPadding - 1));
                Col("┃\n", colour);
            }

            // Disclaimer in red, still centered
            foreach (string line in disclaimer)
            {
                int leftPadding = (boxWidth - line.Length) / 2;
                int rightPadding = boxWidth - line.Length - leftPadding;
                Col("┃", colour);
                Col(new string(' ', leftPadding - 1));
                Col(line, "r");
                Col(new string(' ', rightPadding - 1));
                Col("┃\n", colour);
            }

            // Footer (press key)
            foreach (string line in footer)
            {
                int leftPadding = (boxWidth - line.Length) / 2;
                int rightPadding = boxWidth - line.Length - leftPadding;
                Col("┃", colour);
                Col(new string(' ', leftPadding - 1));
                Col(line, "y");
                Col(new string(' ', rightPadding - 1));
                Col("┃\n", colour);
            }

            // Bottom padding inside the frame
            int bottomPadding = boxHeight - topPadding - contentHeight;
            for (int i = 0; i < bottomPadding - 1; i++)
            {
                Col("┃" + new string(' ', boxWidth - 2) + "┃\n", colour);
            }

            // Bottom border
            Col("┗" + new string('━', boxWidth - 2) + "┛", colour);
            Console.ReadKey();
        }

        /*
         * Main menu loop: routes to salary, expenses, reports, etc.
         * Keeps asking until the user selects a valid option or quits.
         */
        private static void MainMenu()
        {
            string mainMenu;
            DataComposer(); // keep report data in sync at the start

            Title("Main Menu", false);

            bool validMenu = false;
            while (!validMenu)
            {
                ColLine("Select a function.\n", "y");
                string mainMenuGap = new string(' ', 20);
                Col(mainMenuGap); MenuKey("1", "Input salary", "br");
                Col(mainMenuGap); MenuKey("2", "View/edit expenses", "br");
                Col(mainMenuGap); MenuKey("3", "View/save report", "br");
                Col(mainMenuGap); MenuKey("4", "Current tax bands", "br");
                Col(mainMenuGap); MenuKey("5", "Export/import configuration", "br");
                Col(mainMenuGap); MenuKey("7", "About Taxcalc", "br");
                Col(mainMenuGap); MenuKey("9", "Quit TaxCalc", "br");

                mainMenu = Console.ReadLine() ?? "";

                switch (mainMenu)
                {
                    case "1":
                        validMenu = true;
                        SalaryCalc();
                        break;
                    case "2":
                        validMenu = true;
                        ExpensesMgr();
                        break;
                    case "3":
                        validMenu = true;
                        ViewReport();
                        break;
                    case "4":
                        validMenu = true;
                        TaxBands();
                        break;
                    case "5":
                        validMenu = true;
                        ExportImportConfig();
                        break;
                   // case "6":
                   //     validMenu = true;
                   //     DebugZone();
                   //     break;
                    case "7":
                        validMenu = true;
                        ReadMe();
                        break;
                    case "9":
                        validMenu = true;
                        Console.Clear();
                        Console.WriteLine("Slán go fóill!");
                        break;
                    case "":
                        Prompt("No input provided.");
                        break;
                    default:
                        Prompt("Input not valid.");
                        break;
                }
            }
        }

        /*
         * SalaryCalc:
         *  - handle salary input, frequency, and personal details
         *  - calculate Income Tax, USC, and PRSI
         *  - show summary and optionally recalc
         *
         * Uses nested methods so it can loop back into the salary flow.
         */
        private static void SalaryCalc()
        {
            string titleSalaryCalc = "Salary Calculation";
            string frequency;
            double coeffYear = 0;   // factor to convert input into yearly salary

            Console.Clear();
            Title(titleSalaryCalc);

            // Nested function: show salary summary if available, otherwise go straight to calculator
            SalCalcHome();

            void SalCalcHome()
            {
                if (!hasSalary)
                {
                    // No salary calculated yet: send user directly to calculator
                    Col("No tax report generated.", "w", "r", true);
                    Bar(" "); Bar(" ");
                    SalCalculator();
                    return;
                }
                else
                {
                    // Existing salary report view
                    ColLine($"{"Salary Report".ToUpper(),-20}  {"Yearly",11}  {"Monthly",11}  {"Bi-weekly",11}  {"Weekly",11}  {"Pctge",5}", "w", "db");

                    ColLine("Gross salary", "w", "dc");
                    Col($"{"before taxes",-23}", "dgy");
                    ColLine($"{salYear,11:C2}  {salYear / 12,11:C2}  {salYear / 26,11:C2}  {salYear / 52,11:C2}  {"100 %",5:P1}", "w");
                    Bar(" ");

                    ColLine("Taxes", "w", "dc");
                    ColLine($"{"¦ Income Tax",-21}  {incomeTax,11:C2}  {incomeTax / 12,11:C2}  {incomeTax / 26,11:C2}  {incomeTax / 52,11:C2}  {SafeDiv(incomeTax, salYear),5:P1}");
                    ColLine($"{"¦ USC",-21}  {USC,11:C2}  {USC / 12,11:C2}  {USC / 26,11:C2}  {USC / 52,11:C2}  {SafeDiv(USC, salYear),5:P1}");
                    ColLine($"{"¦ PRSI",-21}  {PRSI,11:C2}  {PRSI / 12,11:C2}  {PRSI / 26,11:C2}  {PRSI / 52,11:C2}  {SafeDiv(PRSI, salYear),5:P1}");
                    ColLine($"{"Total",-21}  {totalTax,11:C2}  {totalTax / 12,11:C2}  {totalTax / 26,11:C2}  {totalTax / 52,11:C2}  {SafeDiv(totalTax, salYear),5:P1}", "w");
                    Bar(" ");

                    ColLine("Net salary", "w", "dc");
                    Col($"{"after taxes",-23}", "dgy");
                    ColLine($"{salNet,11:C2}  {salNet / 12,11:C2}  {salNet / 26,11:C2}  {salNet / 52,11:C2}  {SafeDiv(salNet, salYear),5:P1}", "w");
                    Bar(" ");

                    // Show which modifiers affected tax bands
                    ColLine("Modifiers", "w", "dc");

                    string plusIncome;
                    double bandMarriedContext;

                    if (salYear > 0 && partnerIncome > 0)
                    {
                        plusIncome = "two incomes";
                        bandMarriedContext = bandIncomeMarried +
                            (partnerIncome <= partnerCap ? partnerIncome : partnerCap);
                    }
                    else if (salYear == 0 || partnerIncome == 0)
                    {
                        plusIncome = "one income";
                        bandMarriedContext = bandIncomeMarried;
                    }
                    else
                    {
                        plusIncome = "";
                        bandMarriedContext = 0;
                    }

                    string incomeTaxModifierRatio = (isMarried, hasChild) switch
                    {
                        (true, _) => $"Married, {plusIncome}",
                        (false, true) => "Lone parent",
                        (false, false) => $"No modifiers, default cutoff @ {bandIncomeBase:C0}"
                    };

                    double incomeTaxModifier = (isMarried, hasChild) switch
                    {
                        (true, _) => bandIncomeMarried + (partnerIncome <= partnerCap ? partnerIncome : partnerCap),
                        (false, true) => bandIncomeLoneParent,
                        (false, false) => bandIncomeBase
                    };

                    Col("Income tax:".PadRight(12), "dc");
                    Col(incomeTaxModifierRatio.PadRight(30), isMarried || hasChild ? "w" : "dgy");
                    if (isMarried || hasChild)
                        Col($"Cutoff point up by {incomeTaxModifier - bandIncomeBase:C0} to {incomeTaxModifier:C0}");
                    Col("\n");

                    // USC modifiers: self-employment and reduced rates
                    bool selfempUSC = salYear > 100000 && isSelfEmployed;
                    bool reducedUSC = userAge >= 70 || hasMedCard;
                    string underOver = userAge >= 70 ? "Over" : "Under";
                    string withMedCard = hasMedCard ? " with medical card" : "";
                    string USCmodifier = (selfempUSC, reducedUSC) switch
                    {
                        (true, _) => "Self-employment over €100,000",
                        (false, true) => $"{underOver} 70{withMedCard}",
                        (false, false) => "No modifiers, default USC rates"
                    };

                    Col("USC:".PadRight(12), "dc");
                    Col(USCmodifier.PadRight(30), reducedUSC || selfempUSC ? "w" : "dgy");

                    if (selfempUSC)
                    {
                        Col($"{"Rate @ 11% for all income > €100,000"}");
                        reducedUSC = false;
                    }
                    if (reducedUSC) Col($"{"Rate @ 2% for all income above €12,012"}");
                    Col("\n");
                    Bar(" ");

                    // Ask user if they want to recalculate
                    ColLine("Do you want to update your salary report? (y/N)", "y");
                    string updateSalary = PureInput();
                    switch (updateSalary)
                    {
                        case "y" or "yes":
                            Console.Clear();
                            Title(titleSalaryCalc);
                            SalCalculator();
                            break;
                        case "n" or "no" or "":
                            MainMenu();
                            return;
                        default:
                            Prompt("Insert a valid answer.");
                            SalCalcHome();
                            break;
                    }
                }
            }

            /*
             * Full salary calculation flow (nested inside SalaryCalc).
             * Prompts for salary, personal info, and applies the rules.
             */
            void SalCalculator()
            {
                // 1) Ask for gross salary amount (valid non-negative double)
                bool validSalary = false;
                while (!validSalary)
                {
                    ColLine("Input your gross salary.", "y");
                    Console.Write("€");
                    string salaryRaw = (Console.ReadLine() ?? "");

                    // "\" -> back to menu
                    if (CheckToMenu(salaryRaw)) return;

                    if (double.TryParse(salaryRaw, out salary) && salary >= 0)
                    {
                        validSalary = true;
                    }
                    else
                    {
                        Prompt("Invalid input. Please enter a valid number.");
                        continue;
                    }
                }

                // 2) Ask for frequency (yearly/monthly/bi-weekly)
                bool validTimeframe = false;
                while (!validTimeframe)
                {
                    Bar(" ");
                    ColLine($"You inserted a sum of {salary:C2}.", "g");
                    ColLine("Select frequency:\n", "y");

                    MenuKey("Y", "Yearly", "br", true);
                    MenuKey("M", "Monthly", "br");
                    MenuKey("B", "Bi-weekly", "");
                    Bar(" ");

                    frequency = PureInput();

                    if (CheckToMenu(frequency)) return;

                    switch (frequency)
                    {
                        case "y" or "":
                            validTimeframe = true;
                            coeffYear = 1;
                            break;
                        case "m":
                            validTimeframe = true;
                            coeffYear = 12;
                            break;
                        case "b":
                            validTimeframe = true;
                            coeffYear = 26;
                            break;
                        default:
                            Prompt("Invalid input. Please try again.");
                            break;
                    }
                }

                salYear = salary * coeffYear;

                // 3) Ask for marital / child / partner info to determine the income tax band
                Title(titleSalaryCalc);

                bool validMarried = false;
                while (!validMarried)
                {
                    ColLine("Are you married or in a civil union? (y/n)", "y");
                    string marriedInput = PureInput();

                    if (CheckToMenu(marriedInput)) return;

                    switch (marriedInput)
                    {
                        case "y" or "yes":
                            isMarried = true;
                            validMarried = true;
                            break;
                        case "n" or "no":
                            isMarried = false;
                            validMarried = true;
                            break;
                        case "":
                            Prompt("Please answer yes or no.");
                            break;
                        default:
                            Prompt("Invalid input. Please enter y or n.");
                            break;
                    }
                }

                if (!isMarried)
                {
                    // Single: ask if lone parent (for higher standard rate cutoff)
                    bool validHasChild = false;
                    while (!validHasChild)
                    {
                        ColLine("Do you have at least one child? (y/n)", "y");
                        string childInput = PureInput();

                        if (CheckToMenu(childInput)) return;

                        switch (childInput)
                        {
                            case "y" or "yes":
                                hasChild = true;
                                validHasChild = true;
                                break;
                            case "n" or "no":
                                hasChild = false;
                                validHasChild = true;
                                break;
                            case "":
                                Prompt("Please answer yes or no.");
                                break;
                            default:
                                Prompt("Invalid input. Please enter y or n.");
                                break;
                        }
                    }

                    bandIncome = hasChild ? bandIncomeLoneParent : bandIncomeBase;
                }
                else
                {
                    // Married: ask for partner income to compute additional band (up to partnerCap)
                    bool validPartner = false;
                    while (!validPartner)
                    {
                        ColLine("Enter your partner's annual gross income:", "y");
                        Console.Write("€");
                        string partnerRaw = (Console.ReadLine() ?? "");

                        if (CheckToMenu(partnerRaw)) return;

                        if (double.TryParse(partnerRaw, out partnerIncome) && partnerIncome >= 0)
                        {
                            validPartner = true;
                        }
                        else
                        {
                            Prompt("Invalid input. Please enter a valid number.");
                        }
                    }

                    double partnerBonus = partnerIncome <= partnerCap ? partnerIncome : partnerCap;
                    bandIncome = bandIncomeMarried + partnerBonus;
                }

                // 4) Calculate Income Tax (20% below band, 40% above band)
                double rateLow = 0.2, rateHigh = 0.4;
                double incomeLow, incomeHigh;

                if (salYear <= bandIncome)
                {
                    incomeLow = salYear;
                    incomeHigh = 0;
                }
                else
                {
                    incomeLow = bandIncome;
                    incomeHigh = salYear - bandIncome;
                }

                incomeTax = incomeLow * rateLow + incomeHigh * rateHigh;

                // 5) Calculate USC, adjusting for self-employment, age, and med card
                int[] thresholdUSC = { 12012, 28700, 70044, 100000 };
                double[] ratesUSC = { 0.005, 0.02, 0.03, 0.08, 0.08 };

                USC = 0;

                // Ask self-employed only if income > 100k (11% USC top rate for that portion)
                if (salYear > 100000)
                {
                    bool validSelf = false;

                    while (!validSelf)
                    {
                        ColLine("Are you self-employed? (y/n)", "y");
                        string selfInput = PureInput();

                        if (CheckToMenu(selfInput)) return;

                        switch (selfInput)
                        {
                            case "y" or "yes":
                                validSelf = true;
                                isSelfEmployed = true;
                                ratesUSC[4] = 0.11;
                                break;
                            case "n" or "no":
                                validSelf = true;
                                isSelfEmployed = false;
                                break;
                            case "":
                                Prompt("Please answer yes or no.");
                                break;
                            default:
                                Prompt("Invalid input. Please enter y or n.");
                                break;
                        }
                    }
                }

                // Reduced USC rules only apply under €60,000
                if (salYear <= 60000)
                {
                    bool validAge = false;

                    while (!validAge)
                    {
                        ColLine("Enter your age:", "y");
                        string ageInput = PureInput();

                        if (CheckToMenu(ageInput)) return;

                        if (int.TryParse(ageInput, out userAge) && userAge >= 0 && userAge <= 122)
                        {
                            validAge = true;
                        }
                        else
                        {
                            Prompt("Invalid age. Please enter a valid number.");
                        }
                    }

                    if (userAge >= 70)
                    {
                        // Over 70: 2% USC on all income above €12,012
                        ratesUSC[2] = 0.02;
                        ratesUSC[3] = 0.02;
                        ratesUSC[4] = 0.02;
                    }
                    else
                    {
                        // Under 70: check for medical card (similar 2% treatment)
                        bool validMedCard = false;

                        while (!validMedCard)
                        {
                            ColLine("Do you have a medical card? (y/n)", "y");
                            string medCardInput = PureInput();

                            if (CheckToMenu(medCardInput)) return;

                            switch (medCardInput)
                            {
                                case "y" or "yes":
                                    validMedCard = true;
                                    hasMedCard = true;
                                    ratesUSC[2] = 0.02;
                                    ratesUSC[3] = 0.02;
                                    ratesUSC[4] = 0.02;
                                    break;
                                case "n" or "no":
                                    validMedCard = true;
                                    hasMedCard = false;
                                    break;
                                case "":
                                    Prompt("Please answer yes or no.");
                                    break;
                                default:
                                    Prompt("Invalid input. Please enter y or n.");
                                    break;
                            }
                        }
                    }
                }

                // No USC if income <= €13,000 (exempt)
                if (salYear > 13000)
                {
                    double previousThreshold = 0;

                    // Apply banded USC to each band up to the user's income
                    for (int i = 0; i < thresholdUSC.Length; i++)
                    {
                        if (salYear > thresholdUSC[i])
                        {
                            double bandWidth = thresholdUSC[i] - previousThreshold;
                            USC += bandWidth * ratesUSC[i];
                            previousThreshold = thresholdUSC[i];
                        }
                        else
                        {
                            // Partial band for the last one that income falls into
                            double bandWidth = salYear - previousThreshold;
                            USC += bandWidth * ratesUSC[i];
                            break;
                        }
                    }

                    // Apply top rate to remainder if income exceeds highest threshold
                    if (salYear > thresholdUSC[thresholdUSC.Length - 1])
                    {
                        double remainder = salYear - previousThreshold;
                        USC += remainder * ratesUSC[ratesUSC.Length - 1];
                    }
                }

                // 6) PRSI: Class A employees, if weekly > threshold
                int thresholdPRSI = 352;
                double ratePRSI = PRSIswitch ? 0.0435 : 0.042;

                PRSI = 0;
                if (salYear / 52 > thresholdPRSI)
                {
                    PRSI = salYear * ratePRSI;
                }

                // 7) Net salary and final state update
                totalTax = incomeTax + USC + PRSI;
                salNet = salYear - totalTax;

                DataComposer();
                hasSalary = true;

                Prompt("Salary updated!", true);

                SalCalcHome();
            }
        }

        /*
         * Expenses manager:
         *  - shows current expenses
         *  - add / import / edit / delete / wipe
         *
         * Uses nested functions for each action.
         */
        private static void ExpensesMgr()
        {
            string titleExpensesMgr = "Expenses Manager";

            Title(titleExpensesMgr);
            ExpMgrHome();

            void ExpMgrHome()
            {
                DataComposer();

                // Recalculate totals and expense count
                expValueTotal = 0;
                expCount = 0;
                for (int i = 0; i < expMemory; i++)
                {
                    bool blankLabel = string.IsNullOrWhiteSpace(expName[i]);
                    bool blankEntry = blankLabel && expValue[i] == 0;
                    if (!blankEntry) expCount++;
                    expValueTotal += expValue[i];
                }

                bool validCRUD = false;
                while (!validCRUD)
                {
                    // Header row
                    Col($"{"ID",2}  {"Current expenses",-32}" + $"{"Yearly",11}  {"Monthly",11}  {"Weekly",11}  {"%'ge",5}\n", "w", "dr");

                    // Expense rows (alternating colours for readability)
                    for (int i = 0; i < expMemory; i++)
                    {
                        Col($"{i + 1,2}  ", i % 2 == 0 ? "y" : "dy");
                        ColLine($"{expName[i],-30}  {expValue[i],11:C2}  {expValue[i] / 12,11:C2}  {expValue[i] / 52,11:C2}  {SafeDiv(expValue[i], expValueTotal),5:P1}", i % 2 == 0 ? "w" : "gy");
                    }

                    // Totals row
                    Col($"==  {"TOTAL",-30}  {expValueTotal,11:C2}  {expValueTotal / 12,11:C2}  {expValueTotal / 52,11:C2}  {"100 %",5:P1}\n", "w", "dg");
                    Bar(" ");

                    // CRUD menu
                    MenuKey("A", "Add", " ");
                    MenuKey("I", "Import list", " ");
                    MenuKey("E", "Edit", " ");
                    MenuKey("D", "Delete", " ");
                    MenuKey("X", "Wipe list", " ");
                    MenuKey("Enter", "Main menu");
                    Bar(" ");

                    string CRUD = PureInput();
                    if (CheckToMenu(CRUD)) return;

                    switch (CRUD)
                    {
                        case "a":
                            validCRUD = true;
                            AddExpense();
                            break;
                        case "i":
                            validCRUD = true;
                            ImportData();
                            break;
                        case "e":
                            validCRUD = true;
                            EditExpense();
                            break;
                        case "d":
                            validCRUD = true;
                            DeleteExpense();
                            break;
                        case "x":
                            validCRUD = true;
                            WipeExpenses();
                            break;
                        case "":
                            MainMenu();
                            return;
                        default:
                            Prompt("Input not valid.");
                            break;
                    }
                }
            }

            /*
             * Add a new expense:
             *  - label (can be blank)
             *  - amount (non-negative)
             *  - timeframe (Y/M/B/W) converted to yearly
             */
            void AddExpense()
            {
                Title(titleExpensesMgr);

                double newValue = 0;
                int coeffExpYear = 1;

                ColLine("Add new expense label.", "y");
                string newName = Console.ReadLine() ?? "";
                if (CheckToMenu(newName)) return;

                Title(titleExpensesMgr);

                // Ask for value and ensure it's numeric
                bool validExpense = false;
                while (!validExpense)
                {
                    string newNamePrompt =
                        (String.IsNullOrWhiteSpace(newName)) ?
                        "...something" :
                        " " + newName;

                    ColLine($"You added an expense for{newNamePrompt}.", "g");
                    ColLine("Add new expense amount:", "y");
                    Console.Write("€");
                    string rawValue = Console.ReadLine() ?? "";

                    if (CheckToMenu(rawValue)) return;

                    if (double.TryParse(rawValue, out newValue) && newValue >= 0)
                    {
                        validExpense = true;
                    }
                    else
                    {
                        Prompt("Invalid input. Please enter a valid number.");
                        continue;
                    }
                }

                // Empty label + zero value is treated as "no change"
                if (string.IsNullOrWhiteSpace(newName) && newValue == 0.0)
                {
                    Prompt("No new expense added.");
                    ExpMgrHome();
                    return;
                }

                Title(titleExpensesMgr);

                // Ask frequency so we can convert to yearly
                bool validTimeframeExp = false;
                while (!validTimeframeExp)
                {
                    ColLine($"You inserted a sum of {newValue:C2}.", "g");
                    ColLine("Select expense timeframe:", "y");
                    Console.WriteLine
                    (
                        "(Y)early\n" +
                        "(M)onthly\n" +
                        "(B)i-weekly\n" +
                        "(W)eekly"
                    );
                    string frequencyExp = PureInput();

                    if (CheckToMenu(frequencyExp)) return;

                    switch (frequencyExp)
                    {
                        case "y":
                            validTimeframeExp = true;
                            coeffExpYear = 1;
                            break;
                        case "m":
                            validTimeframeExp = true;
                            coeffExpYear = 12;
                            break;
                        case "b":
                            validTimeframeExp = true;
                            coeffExpYear = 26;
                            break;
                        case "w":
                            validTimeframeExp = true;
                            coeffExpYear = 52;
                            break;
                        default:
                            Prompt("Invalid input. Please try again.");
                            break;
                    }
                }

                newValue *= coeffExpYear;

                // Store new expense in first empty slot
                for (int i = 0; i < expMemory; i++)
                {
                    if (String.IsNullOrWhiteSpace(expName[i]) && expValue[i] == 0.0)
                    {
                        expName[i] = newName;
                        expValue[i] = newValue;
                        DataComposer();
                        break;
                    }
                }

                string displayName =
                    string.IsNullOrWhiteSpace(newName) ?
                    "(no label)" :
                    newName;
                Prompt($"New expense added: {displayName}", true);

                ExpMgrHome();
            }

            /*
             * Import expenses from a CSV-like file:
             *  each line: label,value (treated as yearly)
             *  can either overwrite or append to current table
             */
            void ImportData()
            {
                Title(titleExpensesMgr);

                string filePath = "";

                bool validFilename = false;
                while (!validFilename)
                {
                    ColLine("Enter the file path.", "y");
                    filePath = Console.ReadLine() ?? "";
                    if (CheckToMenu(filePath)) return;

                    filePath = filePath.Replace("\\", "/");

                    if (String.IsNullOrWhiteSpace(filePath))
                    {
                        Prompt("File path can't be blank.");
                    }
                    else
                    {
                        if (File.Exists(filePath))
                        {
                            validFilename = true;
                        }
                        else
                        {
                            Prompt("File not found.");
                        }
                    }
                }

                string[] csvRaw = File.ReadAllLines(filePath);

                Title(titleExpensesMgr);

                // Ask whether to overwrite the table or append to it
                bool validMethod = false;
                while (!validMethod)
                {
                    ColLine("File found!", "g");

                    ColLine("Select method:\n", "y");

                    MenuKey("O", "Overwrite existing records", "br");
                    MenuKey("A", "Append to existing records", "br");

                    string importMethod = PureInput();
                    if (CheckToMenu(importMethod)) return;

                    switch (importMethod)
                    {
                        case "o":
                            validMethod = true;

                            // Clear all
                            for (int i = 0; i < expMemory; i++)
                            {
                                expName[i] = null;
                                expValue[i] = 0;
                            }

                            // Insert up to expMemory lines
                            for (int i = 0; i < expMemory && i < csvRaw.Length; i++)
                            {
                                string[] entry = csvRaw[i].Split(",");

                                if (entry.Length == 2 && double.TryParse(entry[1].Trim(), out double value))
                                {
                                    expName[i] = entry[0].Trim();
                                    expValue[i] = value;
                                }
                                else
                                {
                                    expName[i] = null;
                                    expValue[i] = 0;
                                }
                            }
                            break;

                        case "a":
                            validMethod = true;

                            // Append lines into first free slots
                            for (int i = 0; i < csvRaw.Length; i++)
                            {
                                string[] entry = csvRaw[i].Split(",");

                                if (entry.Length == 2 && double.TryParse(entry[1].Trim(), out double value))
                                {
                                    for (int j = 0; j < expMemory; j++)
                                    {
                                        if (String.IsNullOrWhiteSpace(expName[j]) && expValue[j] == 0)
                                        {
                                            expName[j] = entry[0].Trim();
                                            expValue[j] = value;
                                            break;
                                        }
                                    }
                                }
                            }
                            break;

                        default:
                            Prompt("Input not valid.");
                            break;
                    }
                }

                CompactExpenses();
                DataComposer();

                Prompt("Import successful!", true);
                ExpMgrHome();
            }

            /*
             * Edit a single expense item by index.
             * "=" can be used to keep old label or value.
             */
            void EditExpense(bool showTitle = true)
            {
                if (showTitle)
                    Title(titleExpensesMgr);

                double updValue = 0;
                int coeffExpYear = 1;

                ColLine("Select the entry to edit:", "y");

                // List current non-empty entries
                for (int i = 0; i < expMemory; i++)
                {
                    bool blankLabel = string.IsNullOrWhiteSpace(expName[i]);
                    bool blankEntry = blankLabel && expValue[i] == 0;

                    if (!blankEntry)
                    {
                        string tempLabel = blankLabel ? "(no label)" : expName[i];
                        Col($"{i + 1,2}", "g");
                        Col(" - ", "dgy");
                        ColLine($"{tempLabel} ({expValue[i]:C2})");
                    }
                }

                if (expCount == 0)
                {
                    // Nothing to edit if the table is empty
                    Prompt("Table is empty, nothing to edit.");
                    ExpMgrHome();
                    return;
                }

                string expToEdit = (Console.ReadLine() ?? "");
                if (CheckToMenu(expToEdit)) return;

                if (!int.TryParse(expToEdit, out int indexEdit) || indexEdit < 1 || indexEdit > expCount)
                {
                    Prompt($"Please enter a number between 1 and {expCount}.");
                    EditExpense(false);
                    return;
                }

                Title(titleExpensesMgr);

                // Update label
                ColLine("Update expense label.", "y");
                string rawName = Console.ReadLine() ?? "";
                if (CheckToMenu(rawName)) return;
                string updName =
                        rawName == "=" ?
                        expName[indexEdit - 1] :
                        rawName;

                Title(titleExpensesMgr);

                // Update value; "=" keeps old value
                bool validExpense = false;
                string rawValue = "";
                while (!validExpense)
                {
                    string updNamePrompt =
                        (String.IsNullOrWhiteSpace(updName)) ?
                        "...something" :
                        " " + updName;
                    string StillOrNow = rawName == "=" ? "still" : "now";
                    ColLine($"Your expense is {StillOrNow} for{updNamePrompt}.", "g");
                    ColLine("Update expense amount:", "y");
                    Console.Write("€");
                    rawValue = Console.ReadLine() ?? "";

                    if (CheckToMenu(rawValue)) return;
                    string pivotValue = "";
                    if (rawValue == "=") pivotValue = Convert.ToString(expValue[indexEdit - 1]);

                    if (rawValue == "=" || (double.TryParse(pivotValue, out updValue) && updValue >= 0))
                    {
                        validExpense = true;
                    }
                    else
                    {
                        Prompt("Invalid input. Please enter a valid number.");
                        continue;
                    }
                }

                // Allow user to effectively cancel if label and value are unchanged
                if ((rawName == "=" && rawValue == "=") || (string.IsNullOrWhiteSpace(updName) && updValue == 0.0))
                {
                    Prompt("Item was not updated.");
                    ExpMgrHome();
                    return;
                }

                Title(titleExpensesMgr);

                // Ask timeframe for updated value (converts to yearly)
                bool validTimeframeExp = false;
                while (!validTimeframeExp)
                {
                    ColLine($"You updated the sum to {updValue:C2}.", "g");
                    ColLine("Update expense timeframe:\n", "y");

                    MenuKey("Y", "Yearly", "br");
                    MenuKey("M", "Monthly", "br");
                    MenuKey("B", "Bi-weekly", "br");
                    MenuKey("W", "Weekly", "br");

                    Console.WriteLine
                    (
                        "(Y)early\n" +
                        "(M)onthly\n" +
                        "(B)i-weekly\n" +
                        "(W)eekly"
                    );
                    string frequencyExp = PureInput();

                    if (CheckToMenu(frequencyExp)) return;

                    switch (frequencyExp)
                    {
                        case "y":
                            validTimeframeExp = true;
                            coeffExpYear = 1;
                            break;
                        case "m":
                            validTimeframeExp = true;
                            coeffExpYear = 12;
                            break;
                        case "b":
                            validTimeframeExp = true;
                            coeffExpYear = 26;
                            break;
                        case "w":
                            validTimeframeExp = true;
                            coeffExpYear = 52;
                            break;
                        default:
                            Prompt("Invalid input. Please try again.");
                            break;
                    }
                }

                updValue *= coeffExpYear;

                expName[indexEdit - 1] = updName;
                expValue[indexEdit - 1] = updValue;
                DataComposer();

                Prompt($"Expense updated: {updName}", true);

                ExpMgrHome();
            }

            /*
             * Delete one expense by index (with confirmation).
             */
            void DeleteExpense(bool showTitle = true)
            {
                if (showTitle)
                    Title(titleExpensesMgr);

                ColLine("Select the entry to delete:", "y");

                for (int i = 0; i < expMemory; i++)
                {
                    bool blankLabel = string.IsNullOrWhiteSpace(expName[i]);
                    bool blankEntry = blankLabel && expValue[i] == 0;

                    if (!blankEntry)
                    {
                        string tempLabel = blankLabel ? "(no label)" : expName[i];
                        Col($"{i + 1,2}", "r");
                        Col(" - ", "dgy");
                        ColLine($"{tempLabel} ({expValue[i]:C2})");
                    }
                }

                if (expCount == 0)
                {
                    Prompt("Table is empty, nothing to delete.");
                    ExpMgrHome();
                    return;
                }

                string expToDelete = Console.ReadLine() ?? "";
                if (CheckToMenu(expToDelete)) return;

                if (!int.TryParse(expToDelete, out int indexDelete) || indexDelete < 1 || indexDelete > expCount)
                {
                    Prompt($"Please enter a number between 1 and {expCount}.");
                    DeleteExpense(false);
                    return;
                }

                Title(titleExpensesMgr);

                string delName = expName[indexDelete - 1];
                double delValue = expValue[indexDelete - 1];

                Console.WriteLine("You're about to delete the following entry:");
                Bar(" ");
                Col($"{delName} - {delValue:C2}", "dr", "y", true);
                Bar(" "); Bar(" ");
                ColLine("Are you sure? Type \"Y\" (capital)", "y", "dr");
                string confirmDelete = Console.ReadLine() ?? "";

                if (confirmDelete == "Y")
                {
                    expName[indexDelete - 1] = null;
                    expValue[indexDelete - 1] = 0;
                    CompactExpenses();
                    DataComposer();
                    Prompt("Expense deleted.", true);
                }
                else
                {
                    Prompt("Deletion not confirmed.");
                }

                ExpMgrHome();
            }

            /*
             * Deletes all expenses (with explicit confirmation).
             */
            void WipeExpenses(bool showTitle = true)
            {
                if (showTitle)
                    Title(titleExpensesMgr);

                if (expCount == 0)
                {
                    Prompt("Table is empty, nothing to wipe..");
                    ExpMgrHome();
                    return;
                }

                Title(titleExpensesMgr);

                ColLine("You're about to delete the entire expense list.", "r");
                Bar(" ");
                ColLine("Are you sure? Type \"YES\" (capital letter) to confirm", "y", "dr");
                string confirmDelete = (Console.ReadLine() ?? "");

                if (confirmDelete == "YES")
                {
                    for (int i = 0; i < expMemory; i++)
                    {
                        expName[i] = null;
                        expValue[i] = 0;
                    }
                    CompactExpenses();
                    DataComposer();
                    Prompt("All expenses deleted.", true);
                }
                else
                {
                    Prompt("Deletion not confirmed.");
                }

                ExpMgrHome();
            }

            /*
             * Move all non-empty expenses to the top of the arrays
             * and clear the rest (keeps the list compact after deletions).
             */
            void CompactExpenses()
            {
                int writeIndex = 0;

                for (int i = 0; i < expMemory; i++)
                {
                    bool blankLabel = string.IsNullOrWhiteSpace(expName[i]);
                    bool empty = blankLabel && expValue[i] == 0.0;

                    if (!empty)
                    {
                        if (i != writeIndex)
                        {
                            expName[writeIndex] = expName[i];
                            expValue[writeIndex] = expValue[i];
                        }
                        writeIndex++;
                    }
                }

                for (int i = writeIndex; i < expMemory; i++)
                {
                    expName[i] = null;
                    expValue[i] = 0.0;
                }
            }
        }

        /*
         * ViewReport:
         *  - shows combined salary + expenses report on screen
         *  - can save as CSV or nicely formatted TXT
         */
        private static void ViewReport()
        {
            Title($"{"Personal tax and expenses report",-39}" + $"{reportTime.ToString("d MMM @ H:mm"),40}", false);
            ReportMain();

            void ReportMain()
            {
                if (hasSalary && expCount > 0)
                {
                    bool validReport = false;
                    while (!validReport)
                    {
                        string[] buffer = rawReport.Split('\n');
                        string[][] reportContentVideo = new string[buffer.Length][];

                        for (int i = 0; i < buffer.Length; i++)
                        {
                            reportContentVideo[i] = buffer[i].Split(',');
                        }

                        /*
                         * Helper to print a report line with a given "style level":
                         *  1 = main header, 2 = important positive, 3 = totals, 4 = normal items.
                         *  Colours and background vary based on style and whether value is negative.
                         */
                        void ReportLine(string entryName, double entryValue, int styleLevel = 4)
                        {
                            bool negValue = (entryValue < 0);
                            string isDebtBack = negValue ? "dr" : "dg";
                            string isDebtForeL2 = entryValue >= salYear ? "r" : "w";
                            string isDebtForeL3 = entryValue >= salYear ? "r" : "gy";

                            int l = styleLevel - 1;
                            string[] prefix = { "", "", "", "¦ " };
                            string[] fore = { "w", "w", isDebtForeL2, isDebtForeL3 };
                            string[] back = { "dy", isDebtBack, "bk", "bk" };

                            ColLine(
                                $"{(prefix[l] + entryName).PadRight(33)} " +
                                $"{entryValue,12:C2} {entryValue / 12,12:C2} {entryValue / 52,12:C2} {SafeDiv(entryValue, salYear),6:P1}", fore[l], back[l]);
                        }

                        // Report header
                        ColLine($"{"Financial report".ToUpper(),-33} {"Yearly",12} {"Monthly",12} {"Weekly",12} {"Pctge",6}", "w", "dm");

                        ReportLine("Gross Salary", salYear, 1);
                        ReportLine("Income Tax", incomeTax);
                        ReportLine("USC", USC);
                        ReportLine("PRSI", PRSI);
                        ReportLine("Total Taxes", totalTax, 3);
                        ReportLine("Net Salary", salNet, 2);

                        // Expenses
                        for (int i = 0; i < expMemory; i++)
                        {
                            ReportLine(expName[i], expValue[i]);
                        }

                        ReportLine("Total Expenses", expValueTotal, 3);
                        ReportLine("Overall balance", overallBalance, 2);
                        Bar(" ");

                        // Save or go back
                        MenuKey("C", "Save to CSV", " ");
                        MenuKey("T", "Save to TXT", " ");
                        MenuKey("Enter", "Main menu");
                        Bar(" ");

                        string saveReport = PureInput();
                        if (CheckToMenu(saveReport)) return;

                        switch (saveReport)
                        {
                            case "c":
                                // Straight dump of CSV-style data to the Desktop
                                File.WriteAllText(desktopPath + "\\report.csv", rawReport);
                                Prompt("CSV file saved to your desktop!", true);
                                continue;

                            case "t":
                                // Build a box-drawing style text table for nicer printing
                                string[] bufferTXT = rawReport.Split('\n');
                                string[][] reportContentTXT = new string[bufferTXT.Length][];

                                for (int i = 0; i < bufferTXT.Length; i++)
                                {
                                    reportContentTXT[i] = bufferTXT[i].Split(',');
                                }

                                static string TableLine(
                                    string type,
                                    string[] content = null,
                                    int cellLabel = 33,
                                    int cellAmount = 13,
                                    int cellPct = 7
                                )
                                {
                                    return type.Trim().ToLower() switch
                                    {
                                        "top" => "╔═" + new string('═', cellLabel) + "═╤" +
                                                 new string('═', cellAmount) + "═╤" +
                                                 new string('═', cellAmount) + "═╤" +
                                                 new string('═', cellAmount) + "═╤" +
                                                 new string('═', cellPct) + "═╗\n",
                                        "mid" => "╟─" + new string('─', cellLabel) + "─┼" +
                                                 new string('─', cellAmount) + "─┼" +
                                                 new string('─', cellAmount) + "─┼" +
                                                 new string('─', cellAmount) + "─┼" +
                                                 new string('─', cellPct) + "─╢\n",
                                        "bottom" => "╚═" + new string('═', cellLabel) + "═╧" +
                                                    new string('═', cellAmount) + "═╧" +
                                                    new string('═', cellAmount) + "═╧" +
                                                    new string('═', cellAmount) + "═╧" +
                                                    new string('═', cellPct) + "═╝",
                                        "subitem" => $"║ ¦ {content[0].PadRight(cellLabel - 2)} " +
                                                     $"│{double.Parse(content[1]).ToString("C2").PadLeft(cellAmount):C2} " +
                                                     $"│{double.Parse(content[2]).ToString("C2").PadLeft(cellAmount):C2} " +
                                                     $"│{double.Parse(content[3]).ToString("C2").PadLeft(cellAmount):C2} " +
                                                     $"│{double.Parse(content[4]).ToString("P1").PadLeft(cellPct):P1} ║\n",
                                        "item" => $"║ {content[0].PadRight(cellLabel)} " +
                                                  $"│{double.Parse(content[1]).ToString("C2").PadLeft(cellAmount):C2} " +
                                                  $"│{double.Parse(content[2]).ToString("C2").PadLeft(cellAmount):C2} " +
                                                  $"│{double.Parse(content[3]).ToString("C2").PadLeft(cellAmount):C2} " +
                                                  $"│{double.Parse(content[4]).ToString("P1").PadLeft(cellPct):P1} ║\n",
                                        "header" => $"║ {content[0].ToUpper().PadRight(cellLabel)} " +
                                                    $"│{double.Parse(content[1]).ToString("C2").PadLeft(cellAmount):C2} " +
                                                    $"│{double.Parse(content[2]).ToString("C2").PadLeft(cellAmount):C2} " +
                                                    $"│{double.Parse(content[3]).ToString("C2").PadLeft(cellAmount):C2} " +
                                                    $"│{double.Parse(content[4]).ToString("P1").PadLeft(cellPct):P1} ║\n",
                                        _ => "",
                                    };
                                }

                                string TXTReport =
                                $"{"Financial report".ToUpper(),-45} {reportTime.ToString("d MMM @ HH:mm"),45}\n" +
                                TableLine("top") +
                                $"║ {"Item",-33} │ {reportContentTXT[0][1],12:C2} │ {reportContentTXT[0][2],12:C2} │ {reportContentTXT[0][3],12:C2} │ {reportContentTXT[0][4],6:P1} ║\n" +
                                TableLine("mid") +
                                TableLine("header", reportContentTXT[1]) +
                                (TableLine("mid"));

                                for (int i = 2; i <= 4; i++)
                                    TXTReport += TableLine("subitem", reportContentTXT[i]);

                                TXTReport +=
                                TableLine("item", reportContentTXT[5]) +
                                TableLine("mid") +
                                TableLine("header", reportContentTXT[6]) +
                                TableLine("mid");

                                for (int i = 7; i <= 22; i++)
                                    TXTReport += TableLine("subitem", reportContentTXT[i]);

                                TXTReport +=
                                TableLine("item", reportContentTXT[23]) +
                                TableLine("mid") +
                                TableLine("header", reportContentTXT[24]) +
                                TableLine("bottom");

                                File.WriteAllText(desktopPath + "\\report.txt", TXTReport);
                                Prompt("TXT file saved to your desktop!", true);
                                continue;

                            case "":
                                // Back to main menu
                                validReport = true;
                                MainMenu();
                                return;

                            default:
                                Prompt("Input not valid.");
                                continue;
                        }
                    }
                }
                else
                {
                    // Not enough data to build a full report: show a mini checklist
                    void Checklist(bool done, string text)
                    {
                        string sign = done ? "√" : " ";
                        string color = done ? "g" : "r";
                        Col($"[{sign}] ");
                        Col($"{text}\n", color);
                    }
                    Prompt("Not enough information");
                    Checklist(hasSalary, "Salary");
                    Bar(" ");
                    Checklist(expCount > 0, "Expenses");
                    Bar(" ");
                    ColLine("Please complete the task(s) in red before accessing the report.", "r");

                    AnyKey();
                    return;
                }
            }

        }

        /*
         * Displays stylized tax band diagrams for:
         *  - Income Tax
         *  - USC
         *  - PRSI
         *
         * This is more explanatory / visual than strictly functional.
         */
        private static void TaxBands()
        {
            Title($"{"Tax rates in Ireland",-39}" + $"{"Budget 2026 ",40}", false);

            void OfficialTitle(string title)
            {
                Col($"{" " + title,-74}", "y", "dg");
                Col("   ", "w", "w");
                Col("▒▒▒\n\n", "dr", "dy");
            }

            // Income Tax diagram
            OfficialTitle("Income Tax");
            ColLine("%   <<<< standard rate @ 20 <<<<   cutoff point   >>>> higher rate @ 40 >>>>");
            ColLine("0---------------------------|--|--|-----------------------|-------------------->", "g");
            ColLine("€*1,000                     44 48 53                      88");
            ColLine("Cutoffs: single 44k, lone parent 48k, married: 1 income 53k, 2 incomes up to 88k", "dgy");
            Bar(" ");

            // USC diagram
            OfficialTitle("Universal Social Charge (USC)");
            ColLine("%  0.5        2                 3                        8               11");
            ColLine("0······|·---------|---------------------------|-------------------|------------>", "g");
            ColLine("€    12,012     28,700                      70,044             100,000 (self-em)");
            ColLine("Exemption for incomes up to €13,000\n" +
                    "Reduced rate (medical card or over 70, < 60k/yr): 2% on all income > €12,012\n" +
                    "Self employed income: 11% on all income above €100,000", "dgy");
            Bar(" ");

            // PRSI diagram
            OfficialTitle("Pay Related Social Insurance (PRSI)");
            string PRSIdecimals = PRSIswitch ? "35" : "20*";
            ColLine($"%                                     4.{PRSIdecimals}");
            ColLine("0···········------------------------------------------------------------------->", "g");
            ColLine("€                                  whole income");
            Col("Class A employees. Exemption under €352/week", "dgy");
            if (!PRSIswitch) { Col(" * Rate up to 4.35% from 1 October", "dgy"); }
            Col("\n");
            Bar(" ");

            // Links to official sources
            OfficialTitle("Additional information");
            Col(" "); Col(" revenue.ie ", "bk", "gy"); Col("  "); Col(" citizensinformation.ie ", "bk", "gy");
            Bar(" ");
            AnyKey();
        }

        /*
         * Export/import configuration:
         *  - salary/tax values
         *  - tax modifiers (marriage, child, partner income)
         *  - USC bands and personal USC flags
         *  - expenses (names + values)
         *
         * Stored in a simple text file in the current working directory.
         */
        private static void ExportImportConfig()
        {
            Title("Export/import configuration");
            string currentFolder = Directory.GetCurrentDirectory() + "\\";
            string fileName = "config.txt";
            string configPath = currentFolder + fileName;
            ExpImpMain();

            void ExpImpMain()
            {
                bool validConfig = false;
                while (!validConfig)
                {
                    string currentFolder = Directory.GetCurrentDirectory() + "\\";

                    Console.WriteLine("This function will save your configuraton to disk and load it from this folder:");
                    Col(currentFolder + "\n", "w");
                    Bar(" ");

                    ColLine("Select an option.\n", "y");

                    MenuKey("E", "Export configuration to file", "br");
                    MenuKey("I", "Import configuration from file", "");
                    Bar(" ");

                    string configMenu = PureInput();

                    if (CheckToMenu(configMenu)) return;

                    switch (configMenu)
                    {
                        case "e":
                            validConfig = true;
                            ExportConfig();
                            break;
                        case "i":
                            validConfig = true;
                            ImportConfig();
                            break;
                        case "":
                            Prompt("No input provided.");
                            break;
                        default:
                            Prompt("Input not valid.");
                            break;
                    }
                }
            }

            /*
             * Write current state to config.txt.
             * Format is simple CSV lines; not robust but easy to inspect/edit.
             */
            void ExportConfig()
            {
                string expNameList = string.Join(",", expName);
                string expValueList = string.Join(",", expValue);
                string thresholdUSClist = string.Join(",", thresholdUSC);
                string ratesUSClist = string.Join(",", ratesUSC);

                string configFile =
                    $"TaxCalc Config @ {DateTime.Now}\n" +
                    $"{salYear},{incomeTax},{USC},{PRSI},{totalTax},{salNet}\n" +
                    $"{isMarried},{hasChild},{partnerIncome}\n" +
                    thresholdUSClist + "\n" +
                    ratesUSClist + "\n" +
                    $"{isSelfEmployed},{userAge},{hasMedCard}\n" +
                    expNameList + "\n" +
                    expValueList;

                // If there's truly no data, don't bother writing a file
                if (string.IsNullOrWhiteSpace(Convert.ToString(salary)) &&
                    string.IsNullOrWhiteSpace(expNameList) &&
                    string.IsNullOrWhiteSpace(expValueList))
                {
                    Prompt("No data to save");
                    ExpImpMain();
                    return;
                }
                else
                {
                    File.WriteAllText(configPath, configFile);
                    Prompt("Configuration successfully exported!", true);
                    ExpImpMain();
                    return;
                }
            }

            /*
             * Read and parse config.txt back into memory.
             * Assumes the file structure matches ExportConfig.
             */
            void ImportConfig()
            {
                if (File.Exists(configPath))
                {
                    string[]
                        config = File.ReadAllLines(configPath),
                        salArray = config[1].Split(","),
                        incomeTaxArray = config[2].Split(","),
                        thresholdUSCArray = config[3].Split(","),
                        ratesUSCArray = config[4].Split(","),
                        USCArray = config[5].Split(","),
                        expNameArray = config[6].Split(","),
                        expValueArray = config[7].Split(",");

                    // Salary / tax values
                    salYear = double.Parse(salArray[0]);
                    if (salYear > 0) hasSalary = true;
                    incomeTax = double.Parse(salArray[1]);
                    USC = double.Parse(salArray[2]);
                    PRSI = double.Parse(salArray[3]);
                    totalTax = double.Parse(salArray[4]);
                    salNet = double.Parse(salArray[5]);

                    // Income tax modifiers
                    isMarried = bool.Parse(incomeTaxArray[0]);
                    hasChild = bool.Parse(incomeTaxArray[1]);
                    partnerIncome = double.Parse(incomeTaxArray[2]);

                    // USC thresholds
                    for (int i = 0; i < thresholdUSCArray.Length; i++)
                    {
                        thresholdUSC[i] = int.Parse(thresholdUSCArray[i]);
                    }

                    // USC rates
                    for (int i = 0; i < ratesUSCArray.Length; i++)
                    {
                        ratesUSC[i] = double.Parse(ratesUSCArray[i]);
                    }

                    // USC/person modifiers
                    isSelfEmployed = bool.Parse(USCArray[0]);
                    userAge = int.Parse(USCArray[1]);
                    hasMedCard = bool.Parse(USCArray[2]);

                    // Expenses
                    expValueTotal = 0;
                    for (int i = 0; i < expMemory; i++)
                    {
                        expName[i] = expNameArray[i];
                        expValue[i] = double.Parse(expValueArray[i]);
                        expValueTotal += expValue[i];
                    }

                    expCount = 0;
                    for (int i = 0; i < expMemory; i++)
                    {
                        bool blankLabel = string.IsNullOrWhiteSpace(expName[i]);
                        bool blankEntry = blankLabel && expValue[i] == 0;
                        if (!blankEntry) expCount++;
                    }

                    DataComposer();

                    Prompt("Configuration successfully imported!", true);
                    ExpImpMain();
                    return;
                }
                else
                {
                    Prompt("File not found.");
                    ExpImpMain();
                    return;
                }
            }

        }

        /*
         * About/ReadMe screen with description, license, and credits.
         */
        private static void ReadMe()
        {
            Title("About TaxCalc", false);
            ColLine("What is Taxcalc?", "w", "dg");
            ColLine("TaxCalc is a console application that helps you understand your take-home pay");
            ColLine("and manage your personal finances. Built as a learning project by a new coder.");
            Bar(" ");
            ColLine("What does it do?", "w", "dg");
            ColLine("• Calculate Irish income tax, USC, and PRSI (Budget 2026 rates)");
            ColLine("• Track up to 16 expense categories with different timeframes");
            ColLine("• Export detailed financial reports to CSV or formatted TXT files");
            ColLine("• Save and load your configuration for future use");
            Bar(" ");
            ColLine("License", "w", "dg");
            Col(" MIT ", "w", "dr");
            Col(" Free to use, modify, and learn from this code.");
            Bar(" "); Bar(" ");
            Col("┏" + new string('━', barSize - 2) + "┓\n", "r");
            Col("┃", "r"); Col(" DISCLAIMER (yes, again)".PadRight(barSize - 2), "y"); Col("┃\n", "r");
            Col("┃", "r"); Col(" For educational purposes only. Tax calculations may not cover all scenarios.".PadRight(barSize - 2), "w"); Col("┃\n", "r");
            Col("┃", "r"); Col(" Always consult official Revenue sources or a tax professional for advice.".PadRight(barSize - 2), "w"); Col("┃\n", "r");
            Col("┃", "r"); Col(" This software is provided 'as is' without warranty of any kind.".PadRight(barSize - 2), "w"); Col("┃\n", "r");
            Col("┗" + new string('━', barSize - 2) + "┛\n", "r");
            Bar(" ");
            ColLine("CREDITS & RESOURCES", "w", "dg");
            Col("Tax information: ");
            Col("revenue.ie", "g");
            Col(" and ");
            Col("citizensinformation.ie", "g");
            Bar(" "); Bar(" ");
            Col(" Built with C# and .NET ", "w", "dm");
            Col(" and plenty of coffee", "dgy");
            Bar(" "); Bar(" ");
            Col("(mit) Francesco Sannicandro - Dublin, Ireland, Planet Earth");
            Bar(" ");
            AnyKey();
        }

        /*
         * Debug area (intentionally left mostly empty).
         * Handy to drop quick experiments without polluting main logic.
         */
        private static void DebugZone()
        {
            // Nothing to see here.
        }

        /*
         * Helper to build one CSV line for a numeric item:
         *   itemName,Yearly,Monthly,Weekly,PercentageOfSalary
         */
        static string DataLine(string itemName, double itemValue)
        {
            return $"{itemName},{itemValue},{itemValue / 12},{itemValue / 52},{SafeDiv(itemValue, salYear)}\n";
        }

        /*
         * Colored console write with optional padding and background.
         * Color codes are short strings like "g", "dy", "dr", etc.
         */
        static void Col(string text, string colFore = "", string colBack = "", bool padded = false)
        {
            ConsoleColor defaultFore = Console.ForegroundColor;
            ConsoleColor defaultBack = Console.BackgroundColor;

            // Foreground color
            Console.ForegroundColor = colFore.Trim().ToLower() switch
            {
                "black" or "bk" => ConsoleColor.Black,
                "darkblue" or "db" => ConsoleColor.DarkBlue,
                "darkgreen" or "dg" => ConsoleColor.DarkGreen,
                "darkcyan" or "dc" => ConsoleColor.DarkCyan,
                "darkred" or "dr" => ConsoleColor.DarkRed,
                "darkmagenta" or "dm" => ConsoleColor.DarkMagenta,
                "darkyellow" or "dy" => ConsoleColor.DarkYellow,
                "grey" or "gy" => ConsoleColor.Gray,
                "darkgrey" or "dgy" => ConsoleColor.DarkGray,
                "blue" or "b" => ConsoleColor.Blue,
                "green" or "g" => ConsoleColor.Green,
                "cyan" or "c" => ConsoleColor.Cyan,
                "red" or "r" => ConsoleColor.Red,
                "magenta" or "m" => ConsoleColor.Magenta,
                "yellow" or "y" => ConsoleColor.Yellow,
                "white" or "w" => ConsoleColor.White,
                _ => defaultFore,
            };

            // Background color
            Console.BackgroundColor = colBack.Trim().ToLower() switch
            {
                "black" or "bk" => ConsoleColor.Black,
                "darkblue" or "db" => ConsoleColor.DarkBlue,
                "darkgreen" or "dg" => ConsoleColor.DarkGreen,
                "darkcyan" or "dc" => ConsoleColor.DarkCyan,
                "darkred" or "dr" => ConsoleColor.DarkRed,
                "darkmagenta" or "dm" => ConsoleColor.DarkMagenta,
                "darkyellow" or "dy" => ConsoleColor.DarkYellow,
                "grey" or "gy" => ConsoleColor.Gray,
                "darkgrey" or "dgy" => ConsoleColor.DarkGray,
                "blue" or "b" => ConsoleColor.Blue,
                "green" or "g" => ConsoleColor.Green,
                "cyan" or "c" => ConsoleColor.Cyan,
                "red" or "r" => ConsoleColor.Red,
                "magenta" or "m" => ConsoleColor.Magenta,
                "yellow" or "y" => ConsoleColor.Yellow,
                "white" or "w" => ConsoleColor.White,
                _ => defaultBack,
            };

            if (padded) text = " " + text + " ";

            Console.Write(text);

            // Restore colors
            Console.ForegroundColor = defaultFore;
            Console.BackgroundColor = defaultBack;
        }

        /*
         * Same as Col, but adds a newline and optionally fills a full-width background bar.
         */
        static void ColLine(string text, string colFore = "", string colBack = "")
        {
            int colorBarSize = barSize;
            string formattedText =
                String.IsNullOrWhiteSpace(colBack) ?
                $"{text.PadRight(colorBarSize)}\n" :
                $" {text.PadRight(colorBarSize - 1)}\n";
            Col(formattedText, colFore, colBack);
        }

        /*
         * Draws a horizontal bar:
         *  - "dash" = dashed
         *  - "star" = starred
         *  - " " or "blank" = blank line
         *  - "ruler" = position ruler (visual aid while designing)
         */
        static void Bar(string bar)
        {
            switch (bar)
            {
                case "dash" or "-":
                    Console.Write(dashbar);
                    break;
                case "star" or "*":
                    Console.Write(starbar);
                    break;
                case "blank" or " ":
                    Console.Write(blank);
                    break;
                case "ruler" or "123" or "1":
                    Console.WriteLine("0--------1---------2---------3---------4---------5---------6---------7---------8");
                    Console.WriteLine("12345678901234567890123456789012345678901234567890123456789012345678901234567890");
                    break;
                default:
                    Console.WriteLine("");
                    break;
            }
        }

        /*
         * Checks for the special "\" command to go back to the main menu.
         * Returns true if it handled the navigation.
         */
        static bool CheckToMenu(string safeKey)
        {
            if (safeKey.Trim().ToLower() == "\\")
            {
                Console.Clear();
                MainMenu();
                return true;
            }
            return false;
        }

        /*
         * Safe division: returns 0 when denominator is 0 instead of throwing.
         */
        static double SafeDiv(double numerator, double denominator)
        {
            return (denominator == 0) ? 0 : numerator / denominator;
        }

        /*
         * Draws a colored title bar and optional "\ Menu" hint on the right.
         */
        static void Title(string title, bool hasMenu = true)
        {
            Console.Clear();
            if (hasMenu)
            {
                Col($" {title,-39}", "w", "db");
                Col($"{"\\ Menu ",40}\n", "b", "db");
            }
            else
            {
                ColLine($"{title}", "w", "db");
            }
            Bar(" ");
        }

        /*
         * Shows a simple prompt screen, with green for success or red for error/info.
         */
        static void Prompt(string message, bool isGood = false)
        {
            Console.Clear();
            string symbol = isGood ? "√" : "*";
            string background = isGood ? "dg" : "dr";
            ColLine($"{symbol} {message,-47}", "w", background);
            Bar(" ");
        }

        /*
         * Reads input, trims whitespace and lowercases it for easier comparisons.
         */
        static string PureInput()
        {
            return (Console.ReadLine() ?? "").Trim().ToLower();
        }

        /*
         * Renders a small key in the menu, like "[Y] Yearly".
         * pad: "br" = blank + newline, " " = spaces, "" = nothing
         */
        static void MenuKey(string key, string label, string pad = "", bool isDefault = false)
        {
            string padding = pad switch
            {
                "br" => "\n\n",
                " " => "  ",
                _ => ""
            };
            string back = isDefault ? "c" : "y";
            Col($" {key} ", "bk", back);
            Col($" {label}{padding}");
        }

        /*
         * "Press any key to continue" helper that returns to MainMenu.
         */
        static void AnyKey()
        {
            Col(blank + "Press any key to continue.", "y");
            Console.ReadKey();
            Console.Clear();
            MainMenu();
        }
    }
}
