Partner Capital Interest Calculator
----------------------------------
This is a Windows Forms (.NET 6) project. It calculates partner capital interest
from the capital introduction date up to 31st March of the selected financial year.
Compounding options: Monthly, Yearly, Weekly, Daily.
You can add multiple capital events (initial capital, additional capital, reinvested interest) with any date before the FY end.

How to build (on Windows with Visual Studio):
1. Requires Visual Studio 2022 or newer with .NET 6 workloads (or the .NET 6 SDK + an editor).
2. Open the folder in Visual Studio: File -> Open -> Folder and select this project's folder.
3. Or create a new "Windows Forms App" (.NET 6) and replace Program.cs with the provided file, and replace the .csproj file.
4. Build -> Run (F5). The resulting EXE will be in bin\Debug\net6.0-windows\

How to produce a standalone EXE:
Use a self-contained publish if you want to distribute without requiring .NET installed:
  dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o publish
This creates an EXE in the publish folder. You may also sign the EXE and bundle an installer.

If you want, I can:
- Produce a zipped Visual Studio project for you to download (this archive contains the project and source). (Included)
- Guide you step-by-step to build a single EXE using dotnet publish.
- Add features: per-partner compounding, interest reinvest-auto entries, reports with PDF export, print-ready statements, Marathi language UI, or GST-related fields.
