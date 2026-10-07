import 'package:flutter/material.dart';

import '../core/theme/tokens.dart';
import '../core/widgets/state_message.dart';

class SearchPage extends StatefulWidget {
  const SearchPage({super.key});

  @override
  State<SearchPage> createState() => _SearchPageState();
}

class _SearchPageState extends State<SearchPage> {
  final TextEditingController _controller = TextEditingController();

  @override
  void dispose() {
    _controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Search')),
      body: Column(
        children: [
          Padding(
            padding: const EdgeInsets.all(NhSpace.s4),
            child: TextField(
              controller: _controller,
              textInputAction: TextInputAction.search,
              decoration: const InputDecoration(
                hintText: 'Search Nepal Hub',
                prefixIcon: Icon(Icons.search_rounded),
              ),
              onSubmitted: (_) {},
            ),
          ),
          Expanded(
            child: StateMessage(
              tone: StateMessageTone.empty,
              title: 'Search everything in one place',
              description: 'Find businesses, jobs, offers, events, resources and calculators.',
            ),
          ),
        ],
      ),
    );
  }
}
