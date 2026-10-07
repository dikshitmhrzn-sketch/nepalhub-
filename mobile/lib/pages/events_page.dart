import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:provider/provider.dart';

import '../../core/api/api_client.dart';
import '../../core/theme/tokens.dart';
import '../../core/util/formats.dart';
import '../../core/widgets/filter_sheet.dart';
import '../../core/widgets/module_list.dart';
import '../../features/events/api.dart';

class EventsPage extends StatelessWidget {
  const EventsPage({super.key});

  @override
  Widget build(BuildContext context) {
    final api = EventsApi(context.read<ApiClient>());
    return ModuleListScaffold<EventSummary>(
      title: 'Events',
      searchHint: 'Search events',
      emptyTitle: 'No events found',
      emptyDescription: 'Try a different search or clear the filters.',
      location: true,
      filterFields: [
        FilterField('category', 'Category', options: filterOptions(eventCategories)),
        FilterField('sort', 'Sort', options: filterOptions(eventSorts)),
      ],
      loader: (page, search, filters) => api.list(
        page: page,
        search: search,
        province: filters['province'],
        district: filters['district'],
        category: filters['category'],
        sort: filters['sort'],
      ),
      itemBuilder: (context, event, _) => _EventCard(
        event: event,
        onTap: () => context.push('/events/${event.slug}'),
      ),
    );
  }
}

class _EventCard extends StatelessWidget {
  const _EventCard({required this.event, required this.onTap});

  final EventSummary event;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final colors = theme.colorScheme;
    final place = [
      if ((event.venue ?? '').trim().isNotEmpty) event.venue!,
      if (event.districtName.isNotEmpty) event.districtName,
    ].join(', ');
    return Card(
      margin: EdgeInsets.zero,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Padding(
          padding: const EdgeInsets.all(NhSpace.s3),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Text(
                      event.title,
                      style: theme.textTheme.titleSmall?.copyWith(
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                  if (event.isOngoing)
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: NhSpace.s2,
                        vertical: 2,
                      ),
                      decoration: BoxDecoration(
                        color: const Color(0xFF128A4C).withValues(alpha: 0.12),
                        borderRadius: BorderRadius.circular(999),
                      ),
                      child: Text(
                        'Happening now',
                        style: theme.textTheme.labelSmall?.copyWith(
                          color: const Color(0xFF128A4C),
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                ],
              ),
              const SizedBox(height: NhSpace.s1),
              Text(
                humanize(event.category),
                style: theme.textTheme.bodyMedium?.copyWith(color: colors.onSurfaceVariant),
              ),
              const SizedBox(height: NhSpace.s2),
              Row(
                children: [
                  Icon(Icons.schedule_rounded, size: 14, color: colors.onSurfaceVariant),
                  const SizedBox(width: NhSpace.s1),
                  Expanded(
                    child: Text(
                      formatRange(event.startsAt, event.endsAt),
                      style: theme.textTheme.bodySmall?.copyWith(
                        color: colors.onSurfaceVariant,
                      ),
                    ),
                  ),
                ],
              ),
              if (place.isNotEmpty) ...[
                const SizedBox(height: NhSpace.s1),
                Row(
                  children: [
                    Icon(Icons.place_rounded, size: 14, color: colors.onSurfaceVariant),
                    const SizedBox(width: NhSpace.s1),
                    Expanded(
                      child: Text(
                        place,
                        style: theme.textTheme.bodySmall?.copyWith(
                          color: colors.onSurfaceVariant,
                        ),
                      ),
                    ),
                  ],
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
