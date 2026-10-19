-- Mundial Feminino Sub-17 2026 (Marrocos): troca a fonte de api-football (950) para
-- a FIFA (fifa:516:292977), como o Mundial Sub-20 Feminino já faz. A api-football
-- não publica a escalação desses jogos; a FIFA publica (live/football).
--
-- Os 24 times já cadastrados pela api-football ganham o vínculo com o IdTeam da
-- FIFA (é por ele que FifaService/FifaEscalacaoService reconhecem a seleção) e o
-- nome no padrão das seleções de base vindas da FIFA ("Brasil Sub-17 (F)").
-- O casamento é pelo IdApi da api-football, que é o mesmo em dev e em produção.
--
-- Os jogos não são mexidos aqui: depois do script, o "Buscar jogos" da competição
-- reaproveita os 36 jogos existentes (casa por temporada + mandante + visitante) e
-- reescreve o LinkDetalhes para a referência da FIFA. Aí o "Reimportar dados" de
-- cada jogo traz escalação e lances da FIFA.
--
-- Rodar no servidor:
--   sudo -u postgres psql -d projectcomentarista -v ON_ERROR_STOP=1 -f mundial-feminino-sub17-2026-fifa.sql

begin;

with mapa(idapi, idfifa, nome) as (values
    (23375, '1915190', 'Argentina Sub-17 (F)'),
    (23618, '1914198', 'Austrália Sub-17 (F)'),
    (25226, '1915201', 'Brasil Sub-17 (F)'),
    (12504, '1916469', 'Canadá Sub-17 (F)'),
    (20531, '1915194', 'Chile Sub-17 (F)'),
    (20526, '1914196', 'China Sub-17 (F)'),
    (26053, '1914879', 'França Sub-17 (F)'),
    (27115, '1914919', 'Alemanha Sub-17 (F)'),
    (27117, '1915244', 'Gana Sub-17 (F)'),
    (25228, '1914191', 'Japão Sub-17 (F)'),
    (24097, '1924647', 'Quênia Sub-17 (F)'),
    (25229, '1916470', 'México Sub-17 (F)'),
    (20529, '1940430', 'Marrocos Sub-17 (F)'),
    (25230, '1915093', 'Nova Zelândia Sub-17 (F)'),
    (25231, '1915218', 'Nigéria Sub-17 (F)'),
    (23619, '1914188', 'Coreia do Norte Sub-17 (F)'),
    (23625, '1914921', 'Noruega Sub-17 (F)'),
    (25232, '1914796', 'Polônia Sub-17 (F)'),
    (18951, '1914762', 'Porto Rico Sub-17 (F)'),
    (26826, '1945854', 'Samoa Sub-17 (F)'),
    (25233, '1914904', 'Espanha Sub-17 (F)'),
    (18955, '1916472', 'Estados Unidos Sub-17 (F)'),
    (23381, '1915209', 'Venezuela Sub-17 (F)'),
    (24098, '1915239', 'Zâmbia Sub-17 (F)')
)
update times t
set linktransfermarket = 'fifa:' || m.idfifa,
    nome = m.nome
from mapa m
where t.idapi = m.idapi
  and t.ehselecao
  and (t.linktransfermarket is null or t.linktransfermarket = '' or t.linktransfermarket = 'fifa:' || m.idfifa);

-- IdApi fica nulo como no Sub-20: a competição deixa de ser da api-football.
update competicoes
set linktransfermarket = 'fifa:516:292977',
    idapi = null
where linktransfermarket = 'apifoot:950:2026';

commit;
