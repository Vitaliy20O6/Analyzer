# C# Solution Analyzer

📊 Утилита для анализа решений на языке C#.  
Анализирует структуру, вычисляет метрики методов и ищет паттерны проектирования.

## 🔧 Возможности

- Разбор `.sln`-файлов с помощью MSBuild
- Отображение структуры решения:
  - Проекты
  - Папки
  - Файлы
- Анализ всех методов в коде с вычислением метрик:
  - Длина метода (в строках)
  - Цикломатическая сложность
  - Halstead Volume
  - Maintainability Index (MI)
  - Количество параметров
  - Количество локальных переменных
  - Количество операторов и операндов
  - Плотность комментариев
  - Fan-In / Fan-Out
  - NPath Complexity
  - Глубина вложенности
- Детекция паттернов проектирования:
  - Factory Method
  - Abstract Factory
  - Adapter
  - Bridge
  - Singleton
  - Prototype
  - Memento
  - Proxy
  - State
  - Strategy
  - Template Method
  - Visitor

## 🚀 Запуск

1. Склонируй репозиторий
2. Открой проект в Visual Studio или запусти из командной строки

## 💡 Зависимости
- .NET 6 или выше
- Microsoft.Build.Locator
- Roslyn (Microsoft.CodeAnalysis)
