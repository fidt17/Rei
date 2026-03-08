#include "Log.h"
#include <cstdlib>
#include <vector>

namespace rei::common::logging
{
    namespace internal
    {
        std::string EraseAll(std::string value, const std::string_view what)
        {
            size_t pos = value.find(what);
            while (pos != std::string::npos)
            {
                value.erase(pos, what.size());
                pos = value.find(what, pos);
            }

            return value;
        }

        std::string_view ExtractFileName(const std::string_view filePath)
        {
            const size_t slashIndex = filePath.find_last_of("/\\");
            return slashIndex == std::string_view::npos ? filePath : filePath.substr(slashIndex + 1);
        }

        std::string_view Trim(const std::string_view value)
        {
            const size_t begin = value.find_first_not_of(' ');
            if (begin == std::string_view::npos)
            {
                return "";
            }

            const size_t end = value.find_last_not_of(' ');
            return value.substr(begin, end - begin + 1);
        }

        size_t FindArgsStart(const std::string_view signature)
        {
            i32 templateDepth = 0;
            for (size_t i = 0; i < signature.size(); ++i)
            {
                const char ch = signature[i];
                if (ch == '<')
                {
                    templateDepth++;
                    continue;
                }

                if (ch == '>')
                {
                    templateDepth--;
                    continue;
                }

                if (ch == '(' && templateDepth == 0)
                {
                    return i;
                }
            }

            return std::string_view::npos;
        }

        std::string BuildFunctionName(const std::string_view signature)
        {
            const size_t argsIndex = FindArgsStart(signature);
            const auto beforeArgs = Trim(argsIndex == std::string_view::npos ? signature : signature.substr(0, argsIndex));
            if (beforeArgs.empty())
            {
                return "";
            }

            std::string symbol = std::string(beforeArgs);
            i32 reverseTemplateDepth = 0;
            for (size_t i = beforeArgs.size(); i > 0; --i)
            {
                const char ch = beforeArgs[i - 1];
                if (ch == '>')
                {
                    reverseTemplateDepth++;
                    continue;
                }

                if (ch == '<')
                {
                    reverseTemplateDepth--;
                    continue;
                }

                if (ch == ' ' && reverseTemplateDepth == 0)
                {
                    symbol = std::string(beforeArgs.substr(i));
                    break;
                }
            }
            symbol = EraseAll(symbol, "class ");
            symbol = EraseAll(symbol, "struct ");

            std::string stripped;
            stripped.reserve(symbol.size());
            i32 templateDepth = 0;
            for (const char ch : symbol)
            {
                if (ch == '<')
                {
                    templateDepth++;
                    continue;
                }

                if (ch == '>')
                {
                    templateDepth--;
                    continue;
                }

                if (templateDepth == 0)
                {
                    stripped.push_back(ch);
                }
            }

            std::vector<std::string> parts;
            size_t start = 0;
            while (start < stripped.size())
            {
                const size_t pos = stripped.find("::", start);
                if (pos == std::string::npos)
                {
                    parts.emplace_back(stripped.substr(start));
                    break;
                }

                parts.emplace_back(stripped.substr(start, pos - start));
                start = pos + 2;
            }

            if (parts.size() >= 2)
            {
                return std::format("{}::{}", parts[parts.size() - 2], parts.back());
            }

            return stripped;
        }

        std::string BuildSourceDetails(const std::source_location location)
        {
            const auto fileName = ExtractFileName(location.file_name());
            const auto functionName = BuildFunctionName(location.function_name());
            return std::format("{} at {}:{}", functionName, fileName, location.line());
        }

        std::string BuildDetails(const std::string_view extraDetails, const std::source_location location)
        {
            const auto sourceDetails = BuildSourceDetails(location);
            if (extraDetails.empty())
            {
                return sourceDetails;
            }

            return std::format("{} | {}", sourceDetails, extraDetails);
        }

    }

    namespace utility
    {
        std::string SimplifyTypeName(const std::string_view rawTypeName)
        {
            std::string typeName(rawTypeName);
            typeName = internal::EraseAll(typeName, "class ");
            typeName = internal::EraseAll(typeName, "struct ");

            const size_t templatePos = typeName.find('<');
            if (templatePos != std::string::npos)
            {
                typeName = typeName.substr(0, templatePos);
            }

            const size_t nsPos = typeName.rfind("::");
            if (nsPos != std::string::npos)
            {
                typeName = typeName.substr(nsPos + 2);
            }

            return typeName;
        }
        
        std::string FormatSize(const i64 bytes)
        {
            if (bytes < 1024)
            {
                return std::format("{} B", bytes);
            }

            const f64 kb = static_cast<f64>(bytes) / 1024.0;
            if (kb < 1024.0)
            {
                return std::format("{:.2f} KB", kb);
            }

            const f64 mb = kb / 1024.0;
            return std::format("{:.2f} MB", mb);
        }

        std::string FormatDurationMs(const i64 durationMs)
        {
            if (durationMs < 1000)
            {
                return std::format("{} ms", durationMs);
            }

            const f64 seconds = static_cast<f64>(durationMs) / 1000.0;
            return std::format("{:.2f} sec", seconds);
        }
    }

    static LogLevelEnum ParseLogLevel(const std::string& value)
    {
        if (value == "debug" || value == "DEBUG")
        {
            return Debug;
        }
        if (value == "warning" || value == "WARNING" || value == "warn" || value == "WARN")
        {
            return Warning;
        }
        if (value == "error" || value == "ERROR")
        {
            return Error;
        }

        return Debug;
    }

    void Log::Initialize()
    {
        _logger = std::make_shared<Logger>("Core");

        const char* logLevelEnv = std::getenv("REI_LOG_LEVEL");
        if (logLevelEnv != nullptr && logLevelEnv[0] != '\0')
        {
            _logger->SetMinLogLevel(ParseLogLevel(logLevelEnv));
        }
    }

    std::shared_ptr<Logger> Log::GetLogger()
    {
        if (_logger == nullptr)
        {
            Initialize();
        }

        return _logger;
    }
}
