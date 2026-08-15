using System;
using System.Drawing.Text;

class Program {
    static void Main() {
        try {
            var pfc = new PrivateFontCollection();
            pfc.AddFontFile(@"..\..\KhzAnalyzer\Assets\Fonts\Inter-Medium.ttf");
            Console.WriteLine("Medium: " + pfc.Families[0].Name);
            
            var pfc2 = new PrivateFontCollection();
            pfc2.AddFontFile(@"..\..\KhzAnalyzer\Assets\Fonts\Inter-SemiBold.ttf");
            Console.WriteLine("SemiBold: " + pfc2.Families[0].Name);
        } catch (Exception ex) {
            Console.WriteLine("Error: " + ex.Message);
        }
    }
}
