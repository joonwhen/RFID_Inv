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

CREATE TABLE rfid_signals(
    epc varchar(255) NOT NULL,
    antenna_no varchar(10) NOT NULL,
    RSSI varchar(10) NOT NULL,
    Last_Signal timestamp NOT NULL
);

INSERT INTO rfid_signals VALUES 
('E28011606000020DBC31A25C', '1', '1', '2016-06-22 19:10:25')
('E28011606000020DBC32484C', '1', '1', '2016-06-22 19:10:25')
('E28011606000020DBC305A7E', '1', '1', '2016-06-22 19:10:25')
('E28011606000020DBC30981E', '1', '1', '2016-06-22 19:10:25')
('E2000020880101130660506C', '1', '1', '2016-06-22 19:10:25');