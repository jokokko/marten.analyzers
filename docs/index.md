---
layout: default
---

<div class="uk-card uk-card-body">    
<h3>Available Analyzers for <a href="http://jasperfx.github.io/marten/">Marten</a></h3>

<hr class="uk-divider-icon">

<table class="uk-table uk-table-hover">
  <thead>
    <tr>
      <th>ID</th>
      <th>Title</th>
      <th>Severity</th>
      <th>Category</th>
    </tr>
  </thead>
  <tbody>
{% for rule in site.rules %}
    <tr>
      <th>
        {% capture rule_url %}rules/{{ rule.title }}{% endcapture %}
        <a href="{{ rule_url | relative_url }}">
          <code>{{ rule.title }}</code>
        </a>
      </th>
      <td>{{ rule.description }}</td>
      <td>{{ rule.severity }}</td>
      <td>{{ rule.category }}</td>
    </tr>
{% endfor %}
  </tbody>
</table>
</div>