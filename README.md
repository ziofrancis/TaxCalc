# TaxCalc – Irish Salary & Personal Finance Calculator (Console App)

![.NET](https://img.shields.io/badge/.NET-8.0-blue)
![License: MIT](https://img.shields.io/badge/License-MIT-green)
![Platform](https://img.shields.io/badge/Platform-Console-lightgrey)

TaxCalc is a C# console application designed to help users understand their **take-home pay**, estimate Irish tax contributions, and manage recurring **personal expenses** – all in a clean terminal-based interface.

This project was built as a learning exercise, with a focus on readable code, modular design, user experience, and handling real-world logic such as tax bands, USC, PRSI, and expense normalisation.

---

## Features

### 💼 Salary & Tax Calculator
- Supports yearly, monthly, and bi-weekly salary input  
- Calculates:
  - Gross yearly salary  
  - Income Tax (20% / 40% bands)  
  - USC (banded, reduced rate eligibility, self-employed 11% rate above €100k)  
  - PRSI with automatic post-2026 rate switching  
- Adjusts tax bands based on:
  - Marital status  
  - Lone parent status  
  - Partner's income  
  - Age > 70  
  - Medical card  
  - Self-employment  

### 🧾 Expenses Manager
- Add, edit, delete, wipe, or import expenses  
- Supports:
  - Yearly  
  - Monthly  
  - Weekly  
  - Bi-weekly  
- Automatically normalizes values to yearly amounts  
- Up to 16 expense entries  
- Percentage breakdown of total spending  

### 📊 Generated Financial Report
- Displays complete financial summary:
  - Salary breakdown  
  - All taxes  
  - Net income  
  - All expenses  
  - Overall balance  
- Export to:
  - **CSV**  
  - **Formatted TXT** (with box-drawing characters)  

### 💾 Configuration Export & Import
Save and load:
- Salary  
- Tax modifiers  
- Partner's income  
- USC thresholds and rates  
- Personal attributes (age, med card, self-employed)  
- Expenses  

Stored in a human-readable `config.txt`.

### 🎨 Console UI
- Clean titles, colored text, menu highlights  
- ASCII splash screen  
- Structured layout  
- Intuitive navigation  
- `\` returns to main menu anytime  

---

## Usage

### Run the application
```bash
dotnet run
```

### Navigation
Use the shown keys for menu options.  
Press `\` to return to the main menu.  
Press any key when prompted.

---

## File Export Locations
- CSV and TXT reports -> **Desktop**
- config.txt -> **Working directory**

---

## Planned Improvements
- Unit tests  
- More expense categories  
- Cross-platform color normalization  
- JSON config format  

---

## License
**MIT License** – Free to use, modify, and learn from.

---

## Credits  
Built by **Francesco Sannicandro** - Dublin, Ireland, Planet Earth

Tax info reference: [Revenue](revenue.ie), [Citizens Information](citizensinformation.ie)

Built with three C's: curiosity, *caffè* and C#/.NET.
