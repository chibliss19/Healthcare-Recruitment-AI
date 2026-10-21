# AI-Powered Healthcare Recruitment Platform

A three-tier full-stack enterprise application built to streamline healthcare recruitment. This platform integrates traditional relational database management with modern generative AI and vector embeddings to match candidates with medical vacancies intelligently.

## 🚀 Architecture & Tech Stack

*   **Backend:** C#, ASP.NET Core 8.0, RESTful APIs
*   **Frontend:** Blazor WebAssembly
*   **Architecture:** Clean Architecture (Domain, Application, Infrastructure, Presentation layers)
*   **Database & ORM:** MySQL, Entity Framework Core
*   **AI Integration:** 
    *   Local Ollama vector embeddings stored in relational tables.
    *   Gemini API cloud routing for complex reasoning and profile analysis.
*   **Security & Data Integrity:** JWT Authentication, deterministic JSON sanitization (`AiJsonRunner`, `StripCodeFence`), and strict exception handling/guardrails.

## ⚙️ Core Features

*   **Intelligent Profile Matching:** Utilizes LLM APIs and local embeddings to analyze applicant profiles against complex medical job requirements.
*   **Structured AI Guardrails:** Enforces strict JSON output formatting from generative models to ensure stable data ingestion.
*   **Enterprise-Grade Scalability:** Built using decoupled layers to ensure the application remains testable and maintainable as business logic expands.
