# SQL Server DDL Extractor

SQL Server DDL Extractor is a lightweight Windows Forms tool that automates DDL extraction from SQL Server databases. It quickly pulls DDL scripts from a database that’s constantly changing so I can feed them into generative AI tools (like ChatGPT) to help me work smarter with my schema.

## Features

-   **Fast DDL Extraction:** Grab DDLs in seconds from frequently updated databases.
-   **Schema Filtering:** Only list non-system tables (e.g., filter by "dbo").
-   **Clean Output:** Generates simplified, easy-to-read DDL scripts by removing extra settings.
-   **Responsive UI:** Uses async operations so the app stays snappy.

## My Use Case

I built this tool because my database schema changes pretty often, and I needed a quick way to grab the latest DDLs. I then feed those scripts into generative AI tools like ChatGPT to get insights and help manage my database. It saves me tons of time and lets me focus on what really matters.

## Installation

1.  Clone the repository:

    ```bash
    git clone [https://github.com/yourusername/sqlserver-ddl-extractor.git](https://github.com/yourusername/sqlserver-ddl-extractor.git)
    ```

2.  Navigate to the project folder:

    ```bash
    cd sqlserver-ddl-extractor
    ```

3.  Build the project:

    ```bash
    dotnet build
    ```

4.  Run the project:

    ```bash
    dotnet run
    ```

## Usage

1.  Launch the application.
2.  Wait for the tables (filtered by schema) to load.
3.  Pick the tables you want to extract the DDL from.
4.  Click "Extract DDL" to generate and view the simplified scripts.
5.  Copy the output and use it with your AI tools.

## License

This project is licensed under the MIT License. See the `LICENSE` file for details.
