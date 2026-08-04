using Regira.Office.VCards.Abstractions;

namespace Office.VCards.Testing.Samples;

public static class ContentSamples
{
    public static IDictionary<VCardVersion, string> VCards => new Dictionary<VCardVersion, string>
    {
        [VCardVersion.V2_1] = @"BEGIN:VCARD
VERSION:2.1
N:Gump;Forrest;;Mr.
FN:Forrest Gump
ORG:Bubba Gump Shrimp Co.
TITLE:Shrimp Man
PHOTO;GIF:http://www.example.com/dir_photos/my_photo.gif
TEL;WORK;CELL:+1-111-555-1212
TEL;HOME;VOICE:+1-404-555-1212
ADR;WORK;PREF:;;100 Waters Edge;Baytown;LA;30314;United States of America
LABEL;WORK;PREF;ENCODING=QUOTED-PRINTABLE;CHARSET=UTF-8:100 Waters Edge=0D=
 =0ABaytown\, LA 30314=0D=0AUnited States of America
ADR;HOME:;;42 Plantation St.;Baytown;LA;30314;United States of America
LABEL;HOME;ENCODING=QUOTED-PRINTABLE;CHARSET=UTF-8:42 Plantation St.=0D=0A=
 Baytown, LA 30314=0D=0AUnited States of America
EMAIL;HOME:forrestgump@example.com
EMAIL;WORK:forrestgump@example.com
REV:20080424T195243Z
END:VCARD

BEGIN:VCARD
VERSION:2.1
N:Verboven;Bram;;;
FN:Bram Verboven
TEL;CELL;PREF:+32486XXXXXX
TEL;WORK:+32473680571
EMAIL;PREF;HOME:bramverboven@hotmail.com
EMAIL;HOME:bram.verboven@gmail.com
EMAIL;WORK:bram@regira.be
NOTE;ENCODING=QUOTED-PRINTABLE:=4E=50=3A=20=35=33=34=37=34=36=0A=42=62=3A=20=39=33=30=34=0A=57=4B=3A=
=20=38=34=32=36=39=30=0A=54=4B=3A=20=39=37=32=39=0A=5A=33=34=32=37=38=
=39=0A=54=56=3A=20=31=34=34=30=38=39=37=35=39=39
END:VCARD",
        [VCardVersion.V3_0] = @"BEGIN:VCARD
VERSION:3.0
N:Gump;Forrest;;Mr.,;
FN:Forrest Gump
ORG:Bubba Gump Shrimp Co.
TITLE:Shrimp Man
PHOTO;VALUE=URI;TYPE=GIF:http://www.example.com/dir_photos/my_photo.gif
TEL;TYPE=WORK,CELL:+1-111-555-1212
TEL;TYPE=HOME,VOICE:+1-404-555-1212
ADR;TYPE=WORK,PREF:;;100 Waters Edge;Baytown;LA;30314;United States of America
LABEL;TYPE=WORK,PREF:100 Waters Edge\nBaytown\, LA 30314\nUnited States of America
ADR;TYPE=HOME:;;42 Plantation St.;Baytown;LA;30314;United States of America
LABEL;TYPE=HOME:42 Plantation St.\nBaytown\, LA 30314\nUnited States of America
EMAIL;HOME:forrestgump@example.com
EMAIL;WORK:forrestgump@example.com
REV:2008-04-24T19:52:43Z
END:VCARD",
        [VCardVersion.V4_0] = @"BEGIN:VCARD
VERSION:4.0
N:Gump;Forrest;;Mr.;
FN:Forrest Gump
ORG:Bubba Gump Shrimp Co.
TITLE:Shrimp Man
PHOTO;MEDIATYPE=image/gif:http://www.example.com/dir_photos/my_photo.gif
TEL;TYPE=WORK,cell,text;VALUE=uri:tel:+1-111-555-1212
TEL;TYPE=HOME,voice;VALUE=uri:tel:+1-404-555-1212
ADR;TYPE=WORK;PREF=1;LABEL=""100 Waters Edge
Baytown\, LA 30314\, United States of America"":;;100 Waters Edge;Baytown;LA;30314;United States of America
ADR;TYPE=HOME;LABEL=""42 Plantation St.
Baytown\, LA 30314\, United States of America"":;;42 Plantation St.;Baytown;LA;30314;United States of America
EMAIL;HOME:forrestgump@example.com
EMAIL;WORK:forrestgump@example.com
REV:20080424T195243Z
END:VCARD
"
    };
}