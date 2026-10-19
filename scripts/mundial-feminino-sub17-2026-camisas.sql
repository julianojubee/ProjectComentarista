-- Mundial Feminino Sub-17 2026: camisa das jogadoras que ficaram sem número.
-- Elas foram cadastradas pela api-football (gols/lances) antes da escalação, sem
-- camisa; a escalação da FIFA as reconheceu pelo nome mas não gravava o número
-- de quem já existia (corrigido em EspnEscalacaoService.AplicarAtletasAsync).
-- Números conforme a escalação publicada pela FIFA (api.fifa.com live/football),
-- casando pelo time da FIFA + nome. Só preenche quem está sem número e quando
-- ninguém do elenco já usa aquele número.
--
-- Rodar no servidor:
--   sudo -u postgres psql -d projectcomentarista -v ON_ERROR_STOP=1 -f mundial-feminino-sub17-2026-camisas.sql

begin;

with mapa(time_link, nome, camisa) as (values
    ('fifa:1914191', 'A. Ito', 3),
    ('fifa:1914191', 'A. Ota', 17),
    ('fifa:1914191', 'A. Tanaka', 11),
    ('fifa:1914191', 'F. Ito', 10),
    ('fifa:1914191', 'H. Matsushita', 8),
    ('fifa:1914191', 'H. Nishi', 5),
    ('fifa:1914191', 'H. Yotsumoto', 4),
    ('fifa:1914191', 'M. Hanashiro', 14),
    ('fifa:1914191', 'M. Yamaji', 18),
    ('fifa:1914191', 'N. Kurita', 20),
    ('fifa:1914191', 'R. Sumiya', 13),
    ('fifa:1914191', 'S. Morota', 6),
    ('fifa:1914191', 'W. Shimizu', 2),
    ('fifa:1914191', 'Y. Kishida', 9),
    ('fifa:1914879', 'C. Thomas', 18),
    ('fifa:1914879', 'E. Dhalluin', 12),
    ('fifa:1914879', 'E. Prezelin', 11),
    ('fifa:1914879', 'G. Bekoundou', 20),
    ('fifa:1914879', 'L. Motyka', 7),
    ('fifa:1914879', 'M. Ane', 17),
    ('fifa:1914879', 'M. Collin', 9),
    ('fifa:1914879', 'M. Zidi', 10),
    ('fifa:1914879', 'R. Olando', 15),
    ('fifa:1914879', 'S. Yerro', 14),
    ('fifa:1914921', 'D. Wilson Kumi', 17),
    ('fifa:1914921', 'E. Berisha', 16),
    ('fifa:1914921', 'H. Nesse', 9),
    ('fifa:1914921', 'I. Lepore', 6),
    ('fifa:1914921', 'J. Fedeler', 19),
    ('fifa:1914921', 'L. Morch', 18),
    ('fifa:1914921', 'M. Bersas', 13),
    ('fifa:1914921', 'M. Hoem', 10),
    ('fifa:1914921', 'M. Melgard', 11),
    ('fifa:1914921', 'M. Reinhardt', 14),
    ('fifa:1914921', 'S. Mendelin-Norebo', 15),
    ('fifa:1915201', 'Andreyna', 5),
    ('fifa:1915201', 'Gigi Pires', 7),
    ('fifa:1915201', 'Greise Kelly', 13),
    ('fifa:1915201', 'Indy', 20),
    ('fifa:1915201', 'Kaillany Ranifah', 19),
    ('fifa:1915201', 'Marcela Bontorim', 15),
    ('fifa:1915201', 'Nicolly', 9),
    ('fifa:1915201', 'Pepe', 17),
    ('fifa:1915201', 'Sarah Coelho', 6),
    ('fifa:1915201', 'Sofia Gamonal', 11),
    ('fifa:1915209', 'A. Carmona', 15),
    ('fifa:1915209', 'A. Granadillo', 8),
    ('fifa:1915209', 'D. Medina', 4),
    ('fifa:1915209', 'J. Flores', 7),
    ('fifa:1915209', 'K. Alfonzo', 3),
    ('fifa:1915209', 'M. Avila', 2),
    ('fifa:1915209', 'M. Bolsegui', 5),
    ('fifa:1915209', 'M. Briceno', 20),
    ('fifa:1915209', 'N. Bermudez', 19),
    ('fifa:1915209', 'O. Duran', 11),
    ('fifa:1915209', 'V. Nunez', 16),
    ('fifa:1915239', 'D. Mulosha', 4),
    ('fifa:1915239', 'E. Mofya', 11),
    ('fifa:1915239', 'E. Ngabwe', 19),
    ('fifa:1915239', 'F. Kaunda', 12),
    ('fifa:1915239', 'G. Phiri', 6),
    ('fifa:1915239', 'L. Mwenda', 7),
    ('fifa:1915239', 'P. Mwiinga', 10),
    ('fifa:1915239', 'V. Phiri', 20),
    ('fifa:1915244', 'B. Kpentey', 19),
    ('fifa:1915244', 'C. Ashiaku', 14),
    ('fifa:1915244', 'C. Obinsrewuo', 13),
    ('fifa:1915244', 'P. Mensah', 9),
    ('fifa:1916469', 'A. Fairweather', 20),
    ('fifa:1916469', 'A. Giese', 2),
    ('fifa:1916469', 'A. Hennessy', 16),
    ('fifa:1916469', 'B. Boldt', 19),
    ('fifa:1916469', 'B. Schwartzberg', 17),
    ('fifa:1916469', 'C. Scicchitano', 6),
    ('fifa:1916469', 'E. Proulx', 5),
    ('fifa:1916469', 'G. Istocki', 10),
    ('fifa:1916469', 'H. Boora', 15),
    ('fifa:1916469', 'L. Kindel', 7),
    ('fifa:1916469', 'M. Alexis', 11),
    ('fifa:1916469', 'R. Tingley', 9)
)
update jogadores j
set numerocamisa = m.camisa
from mapa m
join times t on t.linktransfermarket = m.time_link
where j.timeid = t.id
  and j.nome = m.nome
  and j.numerocamisa is null
  and not exists (select 1 from jogadores o where o.timeid = t.id and o.numerocamisa = m.camisa);

commit;
