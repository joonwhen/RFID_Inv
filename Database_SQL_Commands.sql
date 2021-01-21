CREATE TABLE rfid_inventory(
    epc varchar(255) NOT NULL,
    tagname varchar(255),
    tagloc varchar(255),
    tagdesc varchar(255),
    tagid varchar(255),
    id integer NOT NULL,
    tagtime timestamp with time zone,
    item_status varchar(255)
);

INSERT INTO rfid_inventory(epc, tagname, tagloc, tagdesc, tagid, id, tagtime, item_status) VALUES('E28011606000020DBC31A25C', 'TAG1', 'tagloc', 'tagdesc', 'tagid', '5', '2004-10-19 10:23:54+02', 'available')
, ('E28011606000020DBC32484C', 'TAG2', 'tagloc', 'tagdesc', 'tagid', '5', '2004-10-19 10:23:54+02', 'available')
, ('E28011606000020DBC305A7E', 'TAG3', 'tagloc', 'tagdesc', 'tagid', '5', '2004-10-19 10:23:54+02', 'available')
, ('E28011606000020DBC30981E', 'TAG4', 'tagloc', 'tagdesc', 'tagid', '5', '2004-10-19 10:23:54+02', 'available')
, ('E2000020880101130660506C', 'CARD', 'tagloc', 'tagdesc', 'tagid', '5', '2004-10-19 10:23:54+02', 'available')
;
