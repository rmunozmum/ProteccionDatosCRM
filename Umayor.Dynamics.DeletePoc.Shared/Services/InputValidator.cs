using System;
using System.Text.RegularExpressions;

namespace Umayor.Dynamics.DeletePoc.Shared.Services;

public class InputValidator
{
    public enum InputType
    {
        RUT,
        Pasaporte,
        Invalido
    }

    public class ValidationResult
    {
        public string RawValue { get; set; } = "";
        public string NormalizedValue { get; set; } = "";
        public string Dv { get; set; } = "";
        public InputType Type { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; } = "";
    }

    public static ValidationResult ValidateAndNormalize(string identifier)
    {
        var result = new ValidationResult { RawValue = identifier };
        if (string.IsNullOrWhiteSpace(identifier))
        {
            result.Type = InputType.Invalido;
            result.IsValid = false;
            result.ErrorMessage = "El identificador está vacío.";
            return result;
        }

        string clean = identifier.Replace(".", "").Replace("-", "").Replace(" ", "").Trim();
        if (clean.Length == 0)
        {
            result.Type = InputType.Invalido;
            result.IsValid = false;
            result.ErrorMessage = "El identificador está vacío.";
            return result;
        }

        // Verificar si parece un RUT chileno (todos dígitos o dígitos seguidos de K/k)
        bool looksLikeRut = Regex.IsMatch(clean, @"^\d+[kK]?$");

        if (looksLikeRut)
        {
            bool explicitDvProvided = identifier.Contains("-") ||
                clean.EndsWith("k", StringComparison.OrdinalIgnoreCase) ||
                clean.Length == 9;

            if (!explicitDvProvided && Regex.IsMatch(clean, @"^\d{7,8}$"))
            {
                result.Type = InputType.RUT;
                result.IsValid = true;
                result.NormalizedValue = clean;
                result.Dv = int.TryParse(clean, out int bodyWithoutDv) ? CalculateDv(bodyWithoutDv) : "";
                return result;
            }

            if (clean.Length < 7 || clean.Length > 10) // RUTs cortos (ej. 5.000.000) o normales
            {
                result.Type = InputType.Invalido;
                result.IsValid = false;
                result.ErrorMessage = $"Largo de RUT incorrecto ({clean.Length} caracteres).";
                return result;
            }

            string dv = clean.Substring(clean.Length - 1).ToUpper();
            string body = clean.Substring(0, clean.Length - 1);

            if (!int.TryParse(body, out int rutNum))
            {
                result.Type = InputType.Invalido;
                result.IsValid = false;
                result.ErrorMessage = "El cuerpo del RUT contiene caracteres no numéricos.";
                return result;
            }

            string calculatedDv = CalculateDv(rutNum);
            if (calculatedDv != dv)
            {
                result.Type = InputType.Invalido;
                result.IsValid = false;
                result.ErrorMessage = $"Dígito verificador inválido. Provisto: '{dv}', Esperado: '{calculatedDv}'.";
                return result;
            }

            result.Type = InputType.RUT;
            result.IsValid = true;
            result.NormalizedValue = body;
            result.Dv = dv;
            return result;
        }

        // Si no es un RUT, verificar si corresponde a un Pasaporte
        // Debe ser alfanumérico y de un largo entre 3 y 20 caracteres
        bool isAlphanumeric = Regex.IsMatch(clean, "^[a-zA-Z0-9]+$");
        if (isAlphanumeric && clean.Length >= 3 && clean.Length <= 20)
        {
            result.Type = InputType.Pasaporte;
            result.IsValid = true;
            result.NormalizedValue = clean;
            result.Dv = "";
            return result;
        }

        result.Type = InputType.Invalido;
        result.IsValid = false;
        result.ErrorMessage = "El formato no corresponde a un RUT válido ni a un Pasaporte alfanumérico.";
        return result;
    }

    public static string CalculateDv(int rut)
    {
        int sum = 0;
        int multiplier = 2;
        while (rut > 0)
        {
            sum += (rut % 10) * multiplier;
            rut /= 10;
            multiplier = multiplier == 7 ? 2 : multiplier + 1;
        }
        int remainder = sum % 11;
        int result = 11 - remainder;
        if (result == 11) return "0";
        if (result == 10) return "K";
        return result.ToString();
    }
}
