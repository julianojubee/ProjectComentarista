-- Libertadores Feminina 2026 (api-football 949): os jogos vieram da API sem grupo
-- e com rodada 0, então a tela não montava a tabela por grupos.
-- Grupos conforme o FotMob. A rodada é a ordem cronológica dentro do grupo
-- (2 jogos por rodada), sem depender de fuso horário.
-- A sincronização não sobrescreve "Group X" nem rodada > 0.
--
-- Rodar no servidor:
--   sudo -u postgres psql -d projectcomentarista -v ON_ERROR_STOP=1 -f libertadores-feminina-2026-grupos.sql

begin;

with mapa(time_nome, grupo) as (values
    ('Corinthians W', 'Group A'), ('Santa Fe W', 'Group A'), ('Colo-Colo W', 'Group A'), ('Caracas W', 'Group A'),
    ('Palmeiras W', 'Group B'), ('Dragonas IDV W', 'Group B'), ('Belgrano W', 'Group B'), ('U. de Deportes W', 'Group B'),
    ('Club Universitario de Deportes', 'Group B'),  -- nome do mesmo time em produção
    ('Cruzeiro W', 'Group C'), ('Bolivar W', 'Group C'), ('Olimpia W', 'Group C'), ('LDU Quito W', 'Group C'),
    ('Deportivo Cali W', 'Group D'), ('Libertad Asuncion W', 'Group D'),
    ('Club Nacional de Football W', 'Group D'), ('Universidad de Chile W', 'Group D')
),
alvo as (
    select j.id, m.grupo,
           ceil(row_number() over (partition by m.grupo order by j.data, j.id) / 2.0)::int as rodada
    from jogos j
    join competicoes c on c.id = j.competicaoid and c.idapi = 949
    join times tc on tc.id = j.timecasaid
    join mapa m on m.time_nome = tc.nome
    where j.temporada = 2026
)
update jogos j set grupo = a.grupo, rodada = a.rodada
from alvo a where a.id = j.id;

-- Aborta se sobrou jogo sem grupo (time com nome diferente em produção).
do $$
declare faltando int;
begin
    select count(*) into faltando
    from jogos j join competicoes c on c.id = j.competicaoid and c.idapi = 949
    where j.temporada = 2026 and (j.grupo is null or j.grupo not like 'Group _');
    if faltando > 0 then
        raise exception '% jogo(s) sem grupo - confira os nomes dos times', faltando;
    end if;
end $$;

select j.grupo, j.rodada, count(*) as jogos
from jogos j join competicoes c on c.id = j.competicaoid and c.idapi = 949
where j.temporada = 2026
group by 1, 2 order by 1, 2;

commit;
