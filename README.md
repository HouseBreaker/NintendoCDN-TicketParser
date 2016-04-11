# PlaiCDN-TitleIdParser
Using PlaiCDN, checks a decrypted title keys file for the legit title keys, looks through 3dsdb for them, and gets info about them.

#Disclaimer
You can use this program to get more info about the title keys of the games you bought in case you need those keys to reacquire them again on the EShop. Obviously this can be used for piracy. I don't care and am not liable for anything piracy-related you do with it.

#Dependencies
.NET Framework 4.5.2

Also, the internet. This downloads stuff it needs if it isn't present.

#Running it
You need [PlaiCDN](https://github.com/Plailect/PlaiCDN/blob/master/PlaiCDN.py) and a copy of [the 3dsdb.com database](http://3dsdb.com/xml.php) in the directory where the exe is. If PlaiCDN or the database aren't there, it'll download it automatically.

Also you need a copy of your decrypted title keys, which you can get using [Decrypt9](https://gbatemp.net/threads/download-decrypt9-open-source-decryption-tools-wip.388831/)

#Credits:
Plailect - for [PlaiCDN](https://github.com/Plailect/PlaiCDN)
Shadowhand - for help with the region parsing code
