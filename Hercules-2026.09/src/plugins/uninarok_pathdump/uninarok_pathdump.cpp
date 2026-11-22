/**
 * Uninarok path dump: a development tool (SPEC-0010, PLAN-0010 4.4, D3). Never listed in conf/plugins.conf: a normal server
 * start does not load it.
 *
 * Pair mode: writes the paths the server's own path->search finds between random cells of a map, then exits.
 *   bin/map-server.exe --load-plugin uninarok_pathdump --uninarok-paths <map>,<seed>,<pairs>,<file>
 *
 * Walk log mode (a normal server run): appends every walk that starts, with the path the server chose, and once a second what
 * each player has in view.
 *   bin/map-server.exe --load-plugin uninarok_pathdump --uninarok-walklog <file>
 *
 * Test rights: gives a local test account group 99 while it is on the map, to be able to warp it.
 *   --uninarok-gm <account id>
 **/

#include "common/hercules.h" /* Should always be the first Hercules file included! (if you don't make it first, you won't be able to use interfaces) */

#include "common/memmgr.h"
#include "common/nullpo.h"
#include "common/showmsg.h"
#include "common/timer.h"
#include "map/battle.h"
#include "map/clif.h"
#include "map/map.h"
#include "map/mob.h"
#include "map/npc.h"
#include "map/path.h"
#include "map/pc.h"
#include "map/status.h"
#include "map/unit.h"

#include "common/HPMDataCheck.h" /* should always be the last Hercules file included! (if you don't make it last, it'll intentionally break compile time) */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

HPM_DECLARE_PLUGIN(
	"uninarok_pathdump", // Plugin name
	SERVER_TYPE_MAP,     // Which server types this plugin works with?
	"1.0"                // Plugin version
)

static char paths_spec[512];
static FILE *walk_log;
static int gm_account;

// ---- pair mode ----

/// The client reads the same numbers from the seed only through the file: any generator will do, this one is xorshift32.
static uint32 xorshift_state;
static uint32 xorshift(void)
{
	uint32 x = xorshift_state;
	x ^= x << 13;
	x ^= x >> 17;
	x ^= x << 5;
	xorshift_state = x;
	return x;
}

static void put8(FILE *fp, int v)
{
	fputc(v & 0xff, fp);
}

static void put16(FILE *fp, int v)
{
	put8(fp, v);
	put8(fp, v >> 8);
}

static void put32(FILE *fp, uint32 v)
{
	put16(fp, (int)(v & 0xffff));
	put16(fp, (int)(v >> 16));
}

/**
 * File (little-endian): "UNPF", version 1 (u32), map name (16 B), xs, ys (u16), seed (u32), pairs (u32), FNV-1a of one byte per
 * cell, 1 = passable (u32). Per pair: x0 y0 x1 y1 (u16), flags (u8: 1 destination passable, 2 A* found, 4 easy path found,
 * 8 straight line free, 16 unit_walk_toxy accepts the walk), A* length (u8), easy length (u8), A* steps, easy steps (u8 each).
 **/
static bool dump_paths(const char *mapname, uint32 seed, int pairs, const char *file)
{
	int id = map->mapname2mapid(mapname);
	if (id < 0) {
		ShowError("uninarok_pathdump: no map '%s'\n", mapname);
		return false;
	}
	struct map_data *md = &map->list[id];
	if (md->cell == (struct mapcell *)0xdeadbeaf)
		map->cellfromcache(md);
	if (md->cell == NULL) {
		ShowError("uninarok_pathdump: '%s' has no cells\n", mapname);
		return false;
	}

	FILE *fp = fopen(file, "wb");
	if (fp == NULL) {
		ShowError("uninarok_pathdump: can not write '%s'\n", file);
		return false;
	}

	char name[16] = { 0 };
	strncpy(name, mapname, sizeof(name) - 1);
	uint32 hash = 2166136261u;
	for (int y = 0; y < md->ys; y++) {
		for (int x = 0; x < md->xs; x++) {
			hash ^= md->getcellp(md, NULL, x, y, CELL_CHKNOPASS) ? 0 : 1;
			hash *= 16777619u;
		}
	}
	fwrite("UNPF", 1, 4, fp);
	put32(fp, 1);
	fwrite(name, 1, sizeof(name), fp);
	put16(fp, md->xs);
	put16(fp, md->ys);
	put32(fp, seed);
	put32(fp, (uint32)pairs);
	put32(fp, hash);

	const int limit = 17, limit_obstructed = 14; // max_walk_path (conf/map/battle/client.conf) and OFFICIAL_WALKPATH
	int count[5] = { 0 };
	xorshift_state = seed != 0 ? seed : 1;
	for (int i = 0; i < pairs; i++) {
		int x0, y0, x1, y1;
		do {
			x0 = (int)(xorshift() % (uint32)md->xs);
			y0 = (int)(xorshift() % (uint32)md->ys);
		} while (md->getcellp(md, NULL, x0, y0, CELL_CHKNOPASS));
		do {
			x1 = x0 + (int)(xorshift() % 41) - 20;
			y1 = y0 + (int)(xorshift() % 41) - 20;
		} while (x1 < 0 || y1 < 0 || x1 >= md->xs || y1 >= md->ys);

		struct walkpath_data astar = { 0 }, easy = { 0 };
		bool passable = !md->getcellp(md, NULL, x1, y1, CELL_CHKNOPASS);
		bool found = path->search(&astar, NULL, id, x0, y0, x1, y1, 0, CELL_CHKNOPASS);
		bool found_easy = path->search(&easy, NULL, id, x0, y0, x1, y1, 1, CELL_CHKNOPASS);
		bool straight = path->search_long(NULL, NULL, id, x0, y0, x1, y1, CELL_CHKNOPASS);
		int len = found ? astar.path_len : 0;
		int len_easy = found_easy ? easy.path_len : 0;
		bool accepted = found && len <= limit && (straight || len <= limit_obstructed);

		put16(fp, x0);
		put16(fp, y0);
		put16(fp, x1);
		put16(fp, y1);
		put8(fp, (passable ? 1 : 0) | (found ? 2 : 0) | (found_easy ? 4 : 0) | (straight ? 8 : 0) | (accepted ? 16 : 0));
		put8(fp, len);
		put8(fp, len_easy);
		fwrite(astar.path, 1, len, fp);
		fwrite(easy.path, 1, len_easy, fp);
		count[0] += found;
		count[1] += found_easy;
		count[2] += passable && !found;
		count[3] += found && !accepted;
		count[4] += accepted;
	}
	fclose(fp);
	ShowStatus("uninarok_pathdump: %s %dx%d cells %08x, %d pairs: %d found, %d easy, %d without a path to a passable cell, %d too long, %d accepted -> %s\n",
		mapname, md->xs, md->ys, hash, pairs, count[0], count[1], count[2], count[3], count[4], file);
	return true;
}

// ---- walk log mode ----

static int (*original_walk_toxy_sub)(struct block_list *bl);

/**
 * W tick map id type easy x y to_x to_y steps
 *   a walk started: the path the server chose, after chase trimming (the packets carry the same two cells)
 **/
static int logged_walk_toxy_sub(struct block_list *bl)
{
	int result = original_walk_toxy_sub(bl);
	struct unit_data *ud = bl != NULL ? unit->bl2ud(bl) : NULL;
	if (result != 0 || ud == NULL || walk_log == NULL || map->list[bl->m].users == 0)
		return result;

	char steps[MAX_WALKPATH + 1];
	int n = 0;
	for (int i = ud->walkpath.path_pos; i < ud->walkpath.path_len && n < MAX_WALKPATH; i++)
		steps[n++] = (char)('0' + ud->walkpath.path[i]);
	steps[n] = '\0';
	fprintf(walk_log, "W %u %s %d %d %d %d %d %d %d %d %s\n", (unsigned int)timer->gettick(), map->list[bl->m].name, bl->id, (int)bl->type,
		(int)ud->state.walk_easy, status->get_speed(bl), bl->x, bl->y, ud->to_x, ud->to_y, n > 0 ? steps : "-");
	return result;
}

/**
 * U id type class x y walking state
 **/
static int log_in_view(struct block_list *bl, va_list ap)
{
	struct block_list *self = va_arg(ap, struct block_list *);
	int *n = va_arg(ap, int *);
	if (bl == self || bl->type == BL_SKILL || bl->type == BL_CHAT)
		return 0;
	struct view_data *vd = status->get_viewdata(bl);
	int cls = vd != NULL ? vd->class_ : 0;
	if (bl->type == BL_ITEM) {
		cls = BL_UCAST(BL_ITEM, bl)->item_data.nameid;
	} else {
		// what clif does not send
		if (vd == NULL || vd->class_ == INVISIBLE_CLASS)
			return 0;
		struct status_change *sc = status->get_sc(bl);
		if (sc != NULL && (sc->option & (OPTION_HIDE | OPTION_CLOAK | OPTION_INVISIBLE)) != 0)
			return 0;
		if (bl->type == BL_NPC && (BL_UCAST(BL_NPC, bl)->option & (OPTION_HIDE | OPTION_INVISIBLE)) != 0)
			return 0;
	}
	struct unit_data *ud = bl->type == BL_ITEM ? NULL : unit->bl2ud(bl); // it asserts on items
	fprintf(walk_log, "U %d %d %d %d %d %d %d\n", bl->id, (int)bl->type, cls, bl->x, bl->y,
		ud != NULL && ud->walktimer != INVALID_TIMER ? 1 : 0, vd != NULL ? (int)vd->dead_sit : 0);
	(*n)++;
	return 1;
}

/**
 * A tick account map x y, the U lines of what is in the player's view, then E count
 **/
static int log_views(int tid, int64 tick, int id, intptr_t data)
{
	if (walk_log == NULL)
		return 0;
	struct s_mapiterator *iter = mapit_getallusers();
	for (struct map_session_data *sd = BL_UCAST(BL_PC, mapit->first(iter)); mapit->exists(iter); sd = BL_UCAST(BL_PC, mapit->next(iter))) {
		if (sd->bl.prev == NULL)
			continue;
		int n = 0;
		fprintf(walk_log, "A %u %d %s %d %d\n", (unsigned int)timer->gettick(), sd->bl.id, map->list[sd->bl.m].name, sd->bl.x, sd->bl.y);
		map->foreachinarea(log_in_view, sd->bl.m, sd->bl.x - AREA_SIZE, sd->bl.y - AREA_SIZE, sd->bl.x + AREA_SIZE, sd->bl.y + AREA_SIZE, BL_ALL, &sd->bl, &n);
		fprintf(walk_log, "E %d\n", n);
	}
	mapit->free(iter);
	fflush(walk_log);
	return 0;
}

// ---- test rights ----

static void (*original_load_end_ack)(int fd, struct map_session_data *sd);

static void load_end_ack_with_rights(int fd, struct map_session_data *sd)
{
	if (gm_account != 0 && sd != NULL && sd->status.account_id == gm_account)
		pc->set_group(sd, 99);
	original_load_end_ack(fd, sd);
}

// ---- the plugin ----

CMDLINEARG(uninarokpaths)
{
	if (params == NULL || strlen(params) >= sizeof(paths_spec))
		return false;
	strcpy(paths_spec, params);
	map->minimal = true;
	return true;
}

CMDLINEARG(uninarokwalklog)
{
	if (params == NULL)
		return false;
	walk_log = fopen(params, "ab");
	if (walk_log == NULL)
		ShowError("uninarok_pathdump: can not write '%s'\n", params);
	return walk_log != NULL;
}

CMDLINEARG(uninarokgm)
{
	gm_account = params != NULL ? atoi(params) : 0;
	return true;
}

HPExport void server_preinit(void)
{
	addArg("--uninarok-paths", true, uninarokpaths, "Writes server paths of a map and exits (usage: --uninarok-paths <map>,<seed>,<pairs>,<file>).");
	addArg("--uninarok-walklog", true, uninarokwalklog, "Logs walks and views to a file (usage: --uninarok-walklog <file>).");
	addArg("--uninarok-gm", true, uninarokgm, "Gives a local test account group 99 (usage: --uninarok-gm <account id>).");
}

// Before clif->init, which copies the parse functions into its packet table.
HPExport void plugin_init(void)
{
	if (gm_account != 0) {
		original_load_end_ack = clif->pLoadEndAck;
		clif->pLoadEndAck = load_end_ack_with_rights;
	}
}

HPExport void server_online(void)
{
	if (paths_spec[0] != '\0') {
		char mapname[64], file[256];
		unsigned int seed;
		int pairs;
		if (sscanf(paths_spec, "%63[^,],%u,%d,%255[^\n]", mapname, &seed, &pairs, file) != 4)
			ShowError("uninarok_pathdump: expected <map>,<seed>,<pairs>,<file>, got '%s'\n", paths_spec);
		else
			dump_paths(mapname, seed, pairs, file);
		return;
	}
	if (walk_log != NULL) {
		original_walk_toxy_sub = unit->walk_toxy_sub;
		unit->walk_toxy_sub = logged_walk_toxy_sub;
		timer->add_func_list(log_views, "uninarok_log_views");
		timer->add_interval(timer->gettick() + 1000, log_views, 0, 0, 1000);
	}
}

HPExport void plugin_final(void)
{
	if (walk_log != NULL) {
		fclose(walk_log);
		walk_log = NULL;
	}
}
