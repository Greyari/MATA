using Microsoft.AspNetCore.Http;

namespace P1F_TPM360_HUB.Function
{
    public class Utility
    {
        public static string ChangeFormatNumber(string str_number, int no_of_comma = 2)
        {
            double number;
            if (double.TryParse(str_number, out number))
            {
                number = Convert.ToDouble(str_number);
            }
            else
            {
                return str_number;
            }
            if (number < 0)
            {
                return "-" + ChangeFormatNumber(str_number.Replace("-", string.Empty), no_of_comma);
            }

            if (no_of_comma == 1)
            {
                return number.ToString("#,##0.0");
            }
            else
            {
                return number.ToString("#,##0.##");
            }
        }

        public static string ShortNumberFormat(string str_number)
        {
            double number;
            if (double.TryParse(str_number, out number))
            {
                number = Convert.ToDouble(str_number);
            }
            else
            {
                return str_number;
            }

            if (number < 0)
            {
                return "-" + ShortNumberFormat(str_number.Replace("-", string.Empty));
            }
            if (number >= 1000000)
            {
                return (number / 1000000).ToString("#,##0.##") + "M";
            }
            if (number >= 1000)
            {
                return (number / 1000).ToString("#,##0.##") + "K";
            }
            return number.ToString("#,##0.##");
        }

        public static string ShortNumberMillion(string str_number)
        {
            double number;
            if (double.TryParse(str_number, out number))
            {
                number = Convert.ToDouble(str_number);
            }
            else
            {
                return str_number;
            }

            if (number < 0)
            {
                return "-" + ShortNumberMillion(str_number.Replace("-", string.Empty));
            }
            if (number >= 10000)
            {
                return (number / 1000000).ToString("#,##0.##") + "M";
            }
            if (number >= 0)
            {
                return (number / 1000).ToString("#,##0.##") + "K";
            }
            return number.ToString("#,##0.##");
        }
        public static string ShortNumberKilo(string str_number)
        {
            double number;
            if (double.TryParse(str_number, out number))
            {
                number = Convert.ToDouble(str_number);
            }
            else
            {
                return str_number;
            }

            if (number < 0)
            {
                return "-" + ShortNumberKilo(str_number.Replace("-", string.Empty));
            }
            if (number >= 0)
            {
                return (number / 1000).ToString("#,##0.##") + "K";
            }
            return number.ToString("#,##0.##");
        }

        public static string ConvertToDateFormat(string str_date, int with_time = 0)
        {
            string result = "";
            if (str_date == "")
            {
                result = "";
            }
            else if (with_time == 1)
            {
                result = Convert.ToDateTime(str_date).ToString("yyyy-MM-dd HH:mm:ss");

            }
            else
            {
                result = Convert.ToDateTime(str_date).ToString("yyyy-MM-dd");
            }
            return result;
        }

        public static string TruncateNumber(string str_number, int no_of_comma = 1)
        {
            double number;
            if (double.TryParse(str_number, out number))
            {
                number = Convert.ToDouble(str_number);
            }
            else
            {
                return str_number;
            }
            if (number < 0)
            {
                return "-" + ChangeFormatNumber(str_number.Replace("-", string.Empty), no_of_comma);
            }

            if (no_of_comma == 1)
            {
                return (Math.Truncate(number * 10) / 10).ToString("0.0");
            }
            else
            {
                return (Math.Truncate(number * 100) / 100).ToString("0.00");
            }
        }
    }
}
